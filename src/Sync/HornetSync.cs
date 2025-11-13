using System;
using System.Collections.Generic;
using GlobalEnums;
using GlobalSettings;
using HarmonyLib;
using SilksongBrothers.Components;
using SilksongBrothers.Network;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SilksongBrothers.Sync;

public class HornetSync : BaseSync
{
    protected override float TriggerFrequency => 15.0f;
    private static HornetSync _instance;

    // self
    private GameObject hornetObject;
    private tk2dSprite hornetSprite;
    private tk2dSpriteAnimator hornetAnimator;
    private Rigidbody2D hornetRigidbody;
    private readonly Dictionary<string, tk2dSpriteCollectionData> _spriteCollections = new();

    // peers
    // peer id => game object
    private readonly Dictionary<string, GameObject> _playerObjects = new();
    private readonly Dictionary<string, tk2dSpriteAnimator> _playerAnimators = new();

    public override void Bind(IConnection connection)
    {
        if (_instance)
            throw new InvalidOperationException("Already bound");
        base.Bind(connection);
        _instance = this;
        connection.AddHandler<HornetPositionPacket>(OnHornetPositionPacket);
        connection.AddHandler<HornetAnimationPacket>(OnHornetAnimationPacket);
        PeerRegistry.AddPeerRemovedHandler(OnPlayerLeave);
    }

    public override void Unbind()
    {
        base.Unbind();
        PeerRegistry.RemovePeerRemovedHandler(OnPlayerLeave);
        _instance = null;
    }

    private void OnPlayerLeave(Peer peer)
    {
        // 另一方面, 玩家可能是在主界面中加入的, 并没有在游戏内加入.
        if (!_playerObjects.TryGetValue(peer.Id, out var playerObject)) return;
        Destroy(playerObject);
        _playerObjects.Remove(peer.Id);
    }

    protected override void Update()
    {
        base.Update();
        if (Connection?.Connected != true) return;
        try
        {
            if (!hornetObject) hornetObject = GameObject.Find("Hero_Hornet");
            if (!hornetObject) hornetObject = GameObject.Find("Hero_Hornet(Clone)");
            if (!hornetObject) return;
            if (!hornetRigidbody)
                hornetRigidbody = hornetObject.GetComponent<Rigidbody2D>();
            if (!hornetSprite)
                hornetSprite = hornetObject.GetComponent<tk2dSprite>();
            if (!hornetAnimator)
                hornetAnimator = hornetObject.GetComponent<tk2dSpriteAnimator>();
            if (_spriteCollections.Count == 0)
                foreach (var c in Resources.FindObjectsOfTypeAll<tk2dSpriteCollectionData>())
                    _spriteCollections[c.spriteCollectionGUID] = c;
        }
        catch (Exception e)
        {
            Utils.Logger?.LogError(e);
        }
    }

    protected override void FixedTrigger()
    {
        if (Connection?.Connected != true) return;
        BroadcastHornetPositionPacket();
    }

    // position
    private void BroadcastHornetPositionPacket()
    {
        if (!hornetObject || !hornetRigidbody) return;

        Connection?.Send(new HornetPositionPacket
        {
            Scene = SceneManager.GetActiveScene().name,
            PosX = hornetObject.transform.position.x,
            PosY = hornetObject.transform.position.y,
            ScaleX = hornetObject.transform.localScale.x,
            VelocityX = hornetRigidbody.linearVelocity.x * Time.timeScale,
            VelocityY = hornetRigidbody.linearVelocity.y * Time.timeScale,
        });
    }

    private void OnHornetPositionPacket(HornetPositionPacket packet)
    {
        try
        {
            if (!hornetObject || !hornetAnimator) return;
            var peerId = packet.SrcPeer;
            var peer = PeerRegistry.Query(peerId);
            if (peer == null) return;

            if (!_playerObjects.TryGetValue(peerId, out var playerObject) || !playerObject)
            {
                playerObject = new GameObject { name = $"SilksongBrother - {peer.Id}" };
                playerObject.transform.SetParent(transform);
                playerObject.transform.position = new Vector3(packet.PosX, packet.PosY,
                    hornetObject.transform.position.z + 0.001f);
                playerObject.transform.localScale = new Vector3(packet.ScaleX, 1, 1);
                _playerObjects[peer.Id] = playerObject;
            }

            if (!playerObject.TryGetComponent<tk2dSprite>(out var playerSprite) || !playerSprite)
            {
                playerSprite = tk2dSprite.AddComponent(playerObject, hornetSprite.Collection, hornetSprite.spriteId);
                playerSprite.color = new Color(1, 1, 1, ModConfig.PlayerOpacity);
            }

            if (!playerObject.TryGetComponent<tk2dSpriteAnimator>(out var playerAnimator) || !playerAnimator)
            {
                playerAnimator = playerObject.AddComponent<tk2dSpriteAnimator>();
                playerAnimator.Library = hornetAnimator.Library;
                playerAnimator.Play(hornetAnimator.CurrentClip);
                _playerAnimators[peer.Id] = playerAnimator;
            }

            if (!playerObject.TryGetComponent<SimpleInterpolator>(out var playerInterpolator) || !playerInterpolator)
            {
                playerInterpolator = playerObject.AddComponent<SimpleInterpolator>();
                playerInterpolator.SetVelocity(new Vector3(packet.VelocityX, packet.VelocityY, 0));
            }

            playerObject.transform.position = new Vector3(packet.PosX, packet.PosY,
                hornetObject.transform.position.z + 0.001f);
            playerObject.transform.localScale = new Vector3(packet.ScaleX, 1, 1);
            playerObject.SetActive(packet.Scene == SceneManager.GetActiveScene().name);
            playerInterpolator.SetVelocity(new Vector3(packet.VelocityX, packet.VelocityY, 0));
        }
        catch (Exception e)
        {
            Utils.Logger?.LogError(e);
        }
    }

    // animation
    private void OnHornetAnimationPacket(HornetAnimationPacket packet)
    {
        try
        {
            if (!hornetObject) return;
            var peerId = packet.SrcPeer;
            var peer = PeerRegistry.Query(peerId);
            if (peer == null) return;

            if (!_playerAnimators.TryGetValue(peer.Id, out var playerAnimator) ||
                !playerAnimator) return;

            var clip = ToolItemManager.GetCrestByName(packet.CrestName)?.HeroConfig
                           ?.GetAnimationClip(packet.ClipName)
                       ?? playerAnimator.Library.GetClipByName(packet.ClipName);
            if (clip == null)
            {
                Utils.Logger?.LogError($"Could not find animation clip {packet.CrestName}/{packet.ClipName}");
                return;
            }

            playerAnimator.Play(clip);
            // Utils.Logger?.LogDebug($"Started animation {clip.name} for player {peer.Name}");
        }
        catch (Exception e)
        {
            Utils.Logger?.LogError(e);
        }
    }

    [HarmonyPatch(typeof(tk2dSpriteAnimator), "Play", typeof(tk2dSpriteAnimationClip), typeof(float), typeof(float))]
    public class HornetAnimationPatch
    {
        // ReSharper disable once InconsistentNaming
        public static void Prefix(tk2dSpriteAnimationClip clip, float clipStartTime, float overrideFps,
            tk2dSpriteAnimator __instance)
        {
            try
            {
                var name = __instance.gameObject.name;
                if (name != "Hero_Hornet" && name != "Hero_Hornet(Clone)") return;

                var crestName = PlayerData.instance?.CurrentCrestID;
                var clipName = clip.name;

                if (crestName == null || clipName == null) return;

                if (_instance)
                    _instance.Connection?.Send(new HornetAnimationPacket
                    {
                        CrestName = crestName,
                        ClipName = clipName,
                    });
            }
            catch (Exception e)
            {
                Utils.Logger?.LogError(e);
            }
        }
    }

    // todo 死亡之后发公告, 公告中报告存活的人数.
    // todo 死亡之后进入幽灵状态
    // todo 幽灵状态时自身隐身 MeshRendered.enabled = false.
    // todo 幽灵状态下不发送自身的位置信息
    // todo 幽灵状态下不接受任何键盘输入, 并且提供观战同步视角.
    // todo 观战同步视角能够跟随其他玩家切换场景 (不需要特殊的切换 scene packet, 直接用 hornetpospacket).
    // todo 观战同步视角能够选择当前存活的玩家.
    // todo 存活玩家坐长椅的时候复活. (需要一个坐长椅 Packet)
    // todo 所有玩家同时进入幽灵状态的时候真正死亡, 所有人的茧都掉落在最后一个存活的玩家死亡位置(这点其实不需要特别设置, 因为观战视角就是剩余的玩家位置).
    // todo 幽灵视角下在屏幕中央添加符合游戏字体的文字提示当前在幽灵状态, 并提示左右移动键切换观战玩家.
    [HarmonyPatch(typeof(PlayerData), "TakeHealth")]
    public static class PatchHcTakeDamage
    {
        private static bool _preventRealDeath = false;

        // 这里不需要考虑幸运骰子的效果, 那个在 HeroController.TakeDamage 中已经判断过了.
        public static bool Prefix(PlayerData __instance, int amount, bool hasBlueHealth, bool allowFracturedMaskBreak)
        {
            var willDie = TakeHealthDryRun(__instance, amount, hasBlueHealth, allowFracturedMaskBreak);
            _preventRealDeath = true;
            return willDie;
            Utils.Logger?.LogDebug("player took damage.");
            if (!_instance) return true;
            _instance.Connection?.Send(new PlayerDeathPacket());
            return true;
        }

        /// <summary>
        /// 判断此伤害是否会导致死亡, 此方法不会修改任何数据.
        /// </summary>
        /// <returns>是否会死亡</returns>
        public static bool TakeHealthDryRun(PlayerData __instance, int amount, bool hasBlueHealth,
            bool allowFracturedMaskBreak)
        {
            var health = __instance.health;
            var healthBlue = __instance.healthBlue;

            if (amount > 0 && health == __instance.maxHealth &&
                health != __instance.CurrentMaxHealth)
            {
                health = __instance.CurrentMaxHealth;
            }

            var leftHealth = health + healthBlue - amount;
            var fracturedMaskTool = Gameplay.FracturedMaskTool;
            if (leftHealth <= 0
                && fracturedMaskTool
                && fracturedMaskTool is
                {
                    IsEquipped: true,
                    SavedData.AmountLeft: > 0
                })
            {
                leftHealth = 1;
            }

            if (leftHealth <= 0)
            {
                leftHealth = ((CheatManager.Invincibility == CheatManager.InvincibilityStates.PreventDeath)
                    ? 1
                    : 0);
            }

            return leftHealth <= 0;
        }
    }
}
