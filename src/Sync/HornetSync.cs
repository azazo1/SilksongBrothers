using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GlobalEnums;
using GlobalSettings;
using HarmonyLib;
using SilksongBrothers.Components;
using SilksongBrothers.Network;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SilksongBrothers.Sync;

enum HornetSyncState
{
    Normal,
    Spectator,
}

public class HornetSync : BaseSync
{
    protected override float TriggerFrequency => 15.0f;
    private static HornetSync _instance;

    private GameObject hornetObject;
    private tk2dSprite hornetSprite;
    private tk2dSpriteAnimator hornetAnimator;
    private Rigidbody2D hornetRigidbody;
    private readonly Dictionary<string, tk2dSpriteCollectionData> _spriteCollections = new();

    // peer id => game object
    private readonly Dictionary<string, GameObject> _playerObjects = new();
    private readonly Dictionary<string, tk2dSpriteAnimator> _playerAnimators = new();

    private HornetSyncState _state = HornetSyncState.Normal;

    /// <summary>
    /// 观察的玩家在 _alivePlayers 中的索引.
    /// </summary>
    private int? _spectatingPlayerIdx;

    /// <summary>
    /// 存活的玩家 peer id, 不包括自身.
    /// </summary>
    private List<string>? _alivePlayers;

    private Vector3 _lastSpectatingPosition;

    public override void Bind(IConnection connection)
    {
        if (_instance)
            throw new InvalidOperationException("Already bound");
        base.Bind(connection);
        _instance = this;
        connection.AddHandler<HornetPositionPacket>(OnHornetPositionPacket);
        connection.AddHandler<HornetAnimationPacket>(OnHornetAnimationPacket);
        connection.AddHandler<PlayerDeathPacket>(OnPeerPlayerDeath);
        connection.AddHandler<RespawnSetPacket>(OnRespawnSet);
        connection.AddHandler<AlivePlayersPacket>(OnAlivePlayers);
        PeerRegistry.AddPeerRemovedHandler(OnPlayerLeave);
    }

    public override void Unbind()
    {
        base.Unbind();
        PeerRegistry.RemovePeerRemovedHandler(OnPlayerLeave);
        _instance = null;
        if (_state == HornetSyncState.Spectator)
        {
            LeaveSpectatorMode();
            RealDie();
        }
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
        if (_state == HornetSyncState.Spectator)
        {
            if (!_alivePlayers.IsNullOrEmpty())
            {
                if (_spectatingPlayerIdx != null)
                {
                    var delta = Input.GetKeyDown(ModConfig.SwitchSpectatingPlayerNextKey) ? 1 : 0;
                    delta -= Input.GetKeyDown(ModConfig.SwitchSpectatingPlayerPreviousKey) ? 1 : 0;
                    _spectatingPlayerIdx += delta;
                    _spectatingPlayerIdx %= _alivePlayers!.Count;
                }
                else
                {
                    _spectatingPlayerIdx = 0;
                }
            }

            KeepSpectatorState();
            var hc = HeroController.instance;
            if (hc)
            {
                hc.transform.position = _lastSpectatingPosition;
            }
        }
        else
        {
            AvoidSpectatorState();
        }


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
        if (_state == HornetSyncState.Spectator) return;

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

    private bool IsSpectating(string peerId)
    {
        if (_state != HornetSyncState.Spectator) return false;
        if (_alivePlayers.IsNullOrEmpty()) return false;
        return _spectatingPlayerIdx is { } idx && peerId == _alivePlayers![idx];
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
            if (IsSpectating(packet.SrcPeer) && hornetObject)
            {
                if (packet.Scene != SceneManager.GetActiveScene().name)
                {
                    ChangeToScene(packet.Scene);
                    if (SceneManager.GetActiveScene().name != packet.Scene) return;
                    hornetObject.transform.position = playerObject.transform.position;
                }
                else
                {
                    hornetObject.transform.position = playerObject.transform.position;
                }

                _lastSpectatingPosition = playerObject.transform.position;
            }
        }
        catch (Exception e)
        {
            Utils.Logger?.LogError(e);
        }
    }

    private static void ChangeToScene(string scene)
    {
        // var transitionPoint = GameManager.instance.FindTransitionPoint(
        //     GameManager.instance.entryGateName,
        //     SceneManager.GetSceneByName(scene), // 场景 (SceneManager.GetSceneByName 获取 scene 之前需要先加载 scene, 不然会获取到 null.)
        //                                         // 但是暂时不知道怎么手动加载场景.
        //     false // fallbackToAnyAvailable == true 则尽管前两个参数是无效的, 仍然可以获得有效的 TransitionPoint.
        // );
        // GameManager.instance.StartCoroutine(hc.EnterScene(
        //     transitionPoint,
        //     0, // 切换场景之后等待 delayBeforeEnter 秒之后走进来.
        //     false, // 暂时未知作用.
        //     onEnd, // 协程调用完毕之后的回调函数.
        //     true // enterSkip == true 忽略走进来的动画, 直接平移角色到入门位置.
        // ));

        // StopAllSceneMusic(GameManager.instance);
        var gm = GameManager.instance;
        if (!gm) return;
        if (gm.GameState == GameState.EXITING_LEVEL) // 防止频繁地切换.
            return;
        gm.ChangeToScene(scene, null, 0);
    }

    // todo 判断是否需要在切换场景时使用这个方法.
    private static void StopAllSceneMusic(GameManager gameManager)
    {
        gameManager.AudioManager.StopAndClearMusic();
        gameManager.AudioManager.StopAndClearAtmos();
        var audio = gameManager.AudioManager.transform;
        var music = audio ? audio.Find("Music") : null;
        if (!music) return;
        var restArea = music.Find("RestArea");
        var playMakerFsm = restArea ? restArea.GetComponent<PlayMakerFSM>() : null;
        if (playMakerFsm)
        {
            playMakerFsm.fsm.Finished = false;
            playMakerFsm.SendEventSafe("REST AREA MUSIC STOP FAST");
        }

        var transform4 = music.Find("FleaCaravan");
        var playMakerFsm2 = transform4 ? transform4.GetComponent<PlayMakerFSM>() : null;
        if (playMakerFsm2)
        {
            playMakerFsm2.fsm.Finished = false;
            playMakerFsm2.SendEventSafe("FLEA MUSIC STOP FAST");
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

    // 也有可能收到自身的死亡 packet.
    private void OnPeerPlayerDeath(PlayerDeathPacket packet)
    {
        var playerName = PeerRegistry.Query(packet.DeadPeerId)?.Name ??
                         (packet.DeadPeerId == Connection?.PeerId ? ModConfig.PlayerName : packet.DeadPeerId);
        SilksongBrothersPlugin.SpawnPopup($"Player: {playerName} died.");
    }

    private void OnAlivePlayers(AlivePlayersPacket packet)
    {
        var aliveCount = "null";
        var hasAlive = true;
        if (packet.AlivePlayers != null)
        {
            aliveCount = packet.AlivePlayers.Count.ToString();
            hasAlive = packet.AlivePlayers.Count > 0;
        }

        _alivePlayers = packet.AlivePlayers?.Where(peerId => peerId != Connection?.PeerId).ToList();
        SilksongBrothersPlugin.SpawnPopup($"Alive players: {aliveCount}.");

        if (_state != HornetSyncState.Spectator) return;
        if (hasAlive) return; // 如果是观战玩家死亡, 切换观战者, 自动在 Update 中进行了切换.
        // 最后的存活玩家死亡, 退出观战模式, 让角色真死亡.
        LeaveSpectatorMode();
        RealDie();
    }

    private static void RealDie()
    {
        PatchTakeHealth.DoInAllowRealDeath(() =>
        {
            PatchTakeDamage.DoInTakingAnyDamage(() =>
            {
                var hc = HeroController.instance;
                if (!hc) return;
                var tmpGo = new GameObject { name = "Spectator Death Force" };
                hc.TakeDamage(tmpGo, CollisionSide.other, 9999, HazardType.NON_HAZARD);
            });
        });
    }

    private void EnterSpectatorMode()
    {
        _state = HornetSyncState.Spectator;
        KeepSpectatorState();
    }

    private void KeepSpectatorState()
    {
        if (!hornetObject) return;
        if (!hornetObject.TryGetComponent<MeshRenderer>(out var mesh) || !mesh) return;
        var hc = HeroController.instance;
        if (!hc) return;
        hc.PauseInput();
        mesh.enabled = false;
        PatchTakeDamage.State = PatchTakeDamage.TakeDamageState.FullInvincibility;
    }

    private void LeaveSpectatorMode()
    {
        _state = HornetSyncState.Normal;
        AvoidSpectatorState();
    }

    private void AvoidSpectatorState()
    {
        if (!hornetObject) return;
        if (!hornetObject.TryGetComponent<MeshRenderer>(out var mesh) || !mesh) return;
        var hc = HeroController.instance;
        if (!hc) return;

        mesh.enabled = true;
        hc.UnPauseInput();
        PatchTakeDamage.State = PatchTakeDamage.TakeDamageState.Normal;
    }

    private void OnRespawnSet(RespawnSetPacket packet)
    {
        if (_state != HornetSyncState.Spectator) return;
        if (!hornetObject) return;
        var hc = HeroController.instance;
        if (!hc) return;

        var facingRight = hornetObject.transform.localScale.x > 0;
        if (SceneManager.GetActiveScene().name != packet.SceneName)
            ChangeToScene(packet.SceneName);
        PatchSitBench.SetRespawnNoSending(
            packet.SpawnMarker,
            packet.SceneName,
            packet.RespawnType,
            facingRight
        );
        hc.transform.position = packet.RespawnPosition;
        hc.SetBenchRespawn(packet.SpawnMarker, packet.SceneName, packet.RespawnType, facingRight); // 坐椅子
        hc.StartCoroutine(hc.Respawn(hc.transform)); // 倒地起身动作
        hc.RefillHealthToMax(); // 回血
        LeaveSpectatorMode();
    }

    [HarmonyPatch(typeof(tk2dSpriteAnimator), nameof(tk2dSpriteAnimator.Play), typeof(tk2dSpriteAnimationClip),
        typeof(float), typeof(float))]
    public class PatchHornetAnimation
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
    // todo 当存活玩家断连时, 算作死亡, 剩余的观战玩家在幽灵状态下死亡.
    // todo 同步机关, 以支持完整的观战视角.
    [HarmonyPatch(typeof(PlayerData), nameof(PlayerData.TakeHealth))]
    public static class PatchTakeHealth
    {
        private static bool _preventRealDeath = true;

        public static void DoInAllowRealDeath(Action action)
        {
            _preventRealDeath = false;
            try
            {
                action();
            }
            finally
            {
                _preventRealDeath = true;
            }
        }

        public static bool Prefix(PlayerData __instance, int amount, bool hasBlueHealth, bool allowFracturedMaskBreak)
        {
            if (!_instance) return true;
            if (!_preventRealDeath) return true;
            var willDie = TakeHealthDryRun(__instance, amount, hasBlueHealth, allowFracturedMaskBreak);
            if (!willDie) return true;

            // 已经进入观战状态则直接拦截.
            if (_instance._state == HornetSyncState.Spectator) return false;
            Utils.Logger?.LogDebug("Entering spectator mode.");
            _instance.EnterSpectatorMode();

            var packet = new PlayerDeathPacket();
            packet.DeadPeerId = packet.SrcPeer;
            _instance.Connection?.Send(packet);
            return false;
        }

        // 这里不需要考虑幸运骰子的效果, 那个在 HeroController.TakeDamage 中已经判断过了.
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
                leftHealth = CheatManager.Invincibility == CheatManager.InvincibilityStates.PreventDeath
                    ? 1
                    : 0;
            }

            return leftHealth <= 0;
        }
    }

    [HarmonyPatch(typeof(GameManager), nameof(GameManager.SetCurrentMapZoneAsRespawn))]
    public static class PatchSitBench
    {
        private static bool _sendingPacket = true;

        public static void SetRespawnNoSending(string spawnMarker, string sceneName,
            int spawnType, bool facingRight)
        {
            var hc = HeroController.instance;
            if (!hc) return;
            _sendingPacket = false;
            hc.SetBenchRespawn(spawnMarker, sceneName, spawnType, facingRight);
            _sendingPacket = true;
        }

        public static void Postfix()
        {
            var spawnMarker = PlayerData.instance.respawnMarkerName;
            var sceneName = PlayerData.instance.respawnScene;
            var respawnType = PlayerData.instance.respawnType;
            Utils.Logger?.LogInfo(
                $"Respawn set: spawnMarker={spawnMarker}, sceneName={sceneName}, respawnType={respawnType}.");
            if (!_instance) return;
            if (!_sendingPacket) return;
            var hc = HeroController.instance;
            if (!hc) return;
            _instance.Connection?.Send(new RespawnSetPacket
            {
                RespawnType = respawnType,
                SceneName = sceneName,
                SpawnMarker = spawnMarker,
                RespawnPosition = hc.transform.position,
            });
        }
    }

    [HarmonyPatch(typeof(HeroController), nameof(HeroController.CanTakeDamage))]
    public static class PatchTakeDamage
    {
        public enum TakeDamageState
        {
            FullInvincibility,
            TakeAnyDamage,
            Normal,
        }

        public static TakeDamageState State = TakeDamageState.Normal;

        public static void DoInTakingAnyDamage(Action action)
        {
            var orig = State;
            State = TakeDamageState.TakeAnyDamage;
            action.Invoke();
            State = orig;
        }

        public static void Postfix(ref bool __result)
        {
            if (State == TakeDamageState.FullInvincibility)
            {
                __result = false;
            }
            else if (State == TakeDamageState.TakeAnyDamage)
            {
                __result = true;
            }
        }
    }
    // todo spectator 模式之下进入新的场景切换都要禁用输入和隐身.
    // todo spectator 模式禁止玩家发出声音.
}
