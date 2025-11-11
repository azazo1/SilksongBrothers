using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MemoryPack;
using SilksongBrothers.Components;
using SilksongBrothers.Network;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SilksongBrothers.Sync;

[MemoryPackable]
public partial class HornetPositionPacket : Packet
{
    public string? Scene;
    public float PosX;
    public float PosY;
    public float ScaleX;
    public float VelocityX;
    public float VelocityY;
}

[MemoryPackable]
public partial class HornetAnimationPacket : Packet
{
    public string? CrestName;
    public string? ClipName;
}

public class HornetSync : Sync
{
    protected override float TriggerFrequency => 15.0f;
    private static HornetSync? _instance;
    private static IConnection? Connection => _instance?._connection;

    // self
    private GameObject hornetObject;
    private tk2dSprite hornetSprite;
    private tk2dSpriteAnimator hornetAnimator;
    private Rigidbody2D hornetRigidbody;
    private readonly Dictionary<string, tk2dSpriteCollectionData> _spriteCollections = new();

    // others
    // peer id => ...
    private readonly Dictionary<string, GameObject> _playerObjects = new();
    private readonly Dictionary<string, tk2dSprite> _playerSprites = new();
    private readonly Dictionary<string, tk2dSpriteAnimator> _playerAnimators = new();
    private readonly Dictionary<string, SimpleInterpolator> _playerInterpolators = new();

    public override void Bind(IConnection connection)
    {
        if (_instance != null)
            throw new InvalidOperationException("Already bound");
        base.Bind(connection);
        _instance = this;
        connection.AddHandler<HornetPositionPacket>(OnHornetPositionPacket);
        connection.AddHandler<HornetAnimationPacket>(OnHornetAnimationPacket);
        PeerRegistry.AddPeerRemovedHandler(OnPlayerLeave);
    }

    public override void Unbind()
    {
        PeerRegistry.RemovePeerRemovedHandler(OnPlayerLeave);
        _instance = null;
    }

    private void OnPlayerLeave(Peer peer)
    {
        if (_playerObjects.TryGetValue(peer.Id, out var playerObject) && playerObject)
            Destroy(playerObject);
    }

    protected override void Update()
    {
        base.Update();
        if (_connection?.Connected != true) return;
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
        if (_connection?.Connected != true) return;
        SendHornetPositionPacket();
    }

    // position
    private void SendHornetPositionPacket()
    {
        if (!hornetObject || !hornetRigidbody) return;

        _connection?.Send(new HornetPositionPacket
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
            if (packet.SrcPeer == null) return;
            var peerId = packet.SrcPeer;
            var peer = PeerRegistry.Query(peerId);
            if (peer == null) return;

            if (!_playerObjects.TryGetValue(peerId, out var playerObject) ||
                !_playerSprites.TryGetValue(peerId, out var playerSprite) ||
                !_playerAnimators.TryGetValue(peerId, out var playerAnimator) ||
                !_playerInterpolators.TryGetValue(peerId, out var playerInterpolator))
            {
                Utils.Logger?.LogDebug($"Creating new player object for player {peer.Name}...");

                playerObject = new GameObject
                {
                    name = $"SilksongBrother - {peer.Name}"
                };
                playerObject.transform.SetParent(transform);
                playerObject.transform.position = new Vector3(packet.PosX, packet.PosY,
                    hornetObject.transform.position.z + 0.001f);
                playerObject.transform.localScale = new Vector3(packet.ScaleX, 1, 1);

                playerSprite = tk2dSprite.AddComponent(playerObject, hornetSprite.Collection,
                    hornetSprite.spriteId);
                playerSprite.color = new Color(1, 1, 1, ModConfig.PlayerOpacity);

                playerAnimator = playerObject.AddComponent<tk2dSpriteAnimator>();
                playerAnimator.Library = hornetAnimator.Library;
                playerAnimator.Play(hornetAnimator.CurrentClip);

                playerInterpolator = playerObject.AddComponent<SimpleInterpolator>();
                playerInterpolator.SetVelocity(new Vector3(packet.VelocityX, packet.VelocityY, 0));

                _playerObjects[peer.Id] = playerObject;
                _playerSprites[peer.Id] = playerSprite;
                _playerAnimators[peer.Id] = playerAnimator;
                _playerInterpolators[peer.Id] = playerInterpolator;
            }

            playerObject.transform.position = new Vector3(packet.PosX, packet.PosY,
                hornetObject.transform.position.z + 0.001f);
            playerObject.transform.localScale = new Vector3(packet.ScaleX, 1, 1);
            playerObject.SetActive(packet.Scene == SceneManager.GetActiveScene().name);
            playerInterpolator.SetVelocity(new Vector3(packet.VelocityX, packet.VelocityY, 0));
            // Utils.Logger?.LogDebug(
            //     $"Updated position of player {packet.ID} to {packet.Scene}/({packet.PositionX} {packet.PositionY})");
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
            if (packet.SrcPeer == null) return;
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
            Utils.Logger?.LogDebug($"Started animation {clip.name} for player {peer.Name}");
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

                Connection?.Send(new HornetAnimationPacket
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
