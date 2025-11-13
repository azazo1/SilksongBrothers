using System;
using System.Collections.Generic;
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
}
