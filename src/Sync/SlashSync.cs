using System;
using GlobalEnums;
using HarmonyLib;
using SilksongBrothers.Network;
using UnityEngine;

namespace SilksongBrothers.Sync;

/// <summary>
/// 同步织针的劈砍特效.
/// </summary>
public class SlashSync : BaseSync
{
    private static SlashSync _instance;
    protected override float TriggerFrequency => 15.0f;
    private GameObject hornetGameObject;

    public override void Bind(IConnection connection)
    {
        if (_instance) throw new InvalidOperationException("Already bound");
        base.Bind(connection);
        _instance = this;
        connection.AddHandler<NailSlashPacket>(OnNailSlash);
    }

    public override void Unbind()
    {
        base.Unbind();
        _instance = null;
    }

    protected override void FixedTrigger()
    {
    }

    /*
     * todo [Error  : Unity Log] NullReferenceException: Object reference not set to an instance of an object
       Stack trace:
       NailAttackBase.OnSlashStarting () (at <35f45ff2c83241c0ab51857388306087>:0)
       NailSlash.StartSlash () (at <35f45ff2c83241c0ab51857388306087>:0)
       SilksongBrothers.Sync.SlashSync.OnNailSlash (SilksongBrothers.NailSlashPacket packet) (at <218c8d47611d4c678ebecdbfe6fc4975>:0)
       SilksongBrothers.Network.Standalone.StandaloneConnection+<>c__DisplayClass29_0`1[T].<AddHandler>b__0 (SilksongBrothers.Packet packet) (at <218c8d47611d4c678ebecdbfe6fc4975>:0)
       SilksongBrothers.Network.Standalone.StandaloneConnection.Update () (at <218c8d47611d4c678ebecdbfe6fc4975>:0)
       SilksongBrothers.Communicator.Update () (at <218c8d47611d4c678ebecdbfe6fc4975>:0)
       SilksongBrothers.SilksongBrothersPlugin.Update () (at <218c8d47611d4c678ebecdbfe6fc4975>:0)
       [Error  : Unity Log] NullReferenceException: Object reference not set to an instance of an object
       Stack trace:
       NailSlashTravel.SetInitialPos () (at <35f45ff2c83241c0ab51857388306087>:0)
       NailSlashTravel.Start () (at <35f45ff2c83241c0ab51857388306087>:0)
     */
    private void OnNailSlash(NailSlashPacket packet)
    {
        var peerId = packet.SrcPeer;
        var playerObject = GameObject.Find(
            $"SilksongBrother - {peerId}" // 这个需要和 HornetSync 一致, 在那边创建 gameobject 在这里引用.
        );
        if (!playerObject) return;
        if (!playerObject.TryGetComponent<HeroController>(out var heroController))
        {
            heroController = playerObject.AddComponent<HeroController>();
            heroController.IgnoreInput(); // 防止按键触发同伴大黄蜂的移动.
        }

        if (!playerObject.TryGetComponent<NailSlash>(out var nailSlash))
        {
            nailSlash = playerObject.AddComponent<NailSlash>();
            nailSlash.hc = heroController;
        }

        Utils.Logger?.LogInfo("Nail slash trigger.");
        nailSlash.StartSlash();
    }

    [HarmonyPatch(typeof(HeroController), "Attack")]
    public static class PatchHeroControllerAttack
    {
        public static void Postfix(HeroController __instance, AttackDirection attackDir)
        {
            if (!_instance) return;
            _instance.Connection?.Send(new NailSlashPacket { Direction = attackDir });
        }
    }
}
