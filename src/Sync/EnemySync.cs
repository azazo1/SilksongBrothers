using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using HutongGames.PlayMaker;
using SilksongBrothers.Network;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace SilksongBrothers.Sync;

// todo 同步不存在的敌人, 比如玩家 a 打死了, 但是玩家 b 重新进入这个 scene, 那么玩家 a 需要接收玩家 b 新创建的敌人.
// todo 检查怪物的血量是否能够正常同步.

public class SyncDeadMarker : MonoBehaviour;

public class LockTransformPosition : MonoBehaviour
{
    public Vector3 lockedPos;
    public bool locked = true;
    private float _lastUpdateTime;

    public void UpdateLockedPos(Vector3 pos)
    {
        lockedPos = pos;
        _lastUpdateTime = Time.time;
        locked = true;
    }

    private void LateUpdate()
    {
        if (!locked)
            return;
        if (Time.time - (double)_lastUpdateTime > 1.0)
            locked = false;
        else
            transform.position = lockedPos;
    }
}

public static class EnemyRegistry
{
    private static readonly Dictionary<string, GameObject> id2go = new();
    private static readonly Dictionary<GameObject, string> go2id = new();
    private static readonly Throttler refreshThrottler = new(500);

    public static void RefreshAllIds()
    {
        id2go.Clear();
        go2id.Clear();
        var healthManagerList = new List<HealthManager>(HealthManager.EnumerateActiveEnemies());
        foreach (var healthManager in healthManagerList.Where(hm => hm))
        {
            GetOrAssignId(healthManager.gameObject);
        }

        foreach (var hm in
                 Object.FindObjectsByType<HealthManager>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None)
                     .Where(hm => hm)
                )
        {
            GetOrAssignId(hm.gameObject);
        }
    }

    public static string? GetOrAssignId(GameObject go)
    {
        if (!go)
            return null;
        if (go2id.TryGetValue(go, out var id))
            return id;
        var key = BuildStableId(go);
        id2go[key] = go;
        go2id[go] = key;
        return key;
    }

    public static GameObject FindById(string? id, string expectedScene)
    {
        if (string.IsNullOrEmpty(id))
            return null;
        if (id2go.TryGetValue(id, out var go) && go)
            return go;
        var name = SceneManager.GetActiveScene().name;
        if (expectedScene != name)
            return null;
        if (!refreshThrottler.Tick()) return null;
        RefreshAllIds();
        return id2go.TryGetValue(id, out go) && go
            ? go
            : null;
    }

    private static string BuildStableId(GameObject go)
    {
        var scene1 = go.scene;
        string str;
        if (!scene1.IsValid())
        {
            str = "noscn";
        }
        else
        {
            var scene2 = go.scene;
            str = scene2.name;
        }

        return $"{str}:{GetPath(go.transform)}";
    }

    private static string GetPath(Transform t)
    {
        var stringList = new List<string>();
        for (var transform = t; transform; transform = transform.parent)
        {
            stringList.Add($"{transform.name}#{transform.GetSiblingIndex().ToString()}");
        }

        stringList.Reverse();
        return string.Join("/", stringList.ToArray());
    }
}

public class EnemySync : BaseSync
{
    private string? _host;
    private bool IsHost => _host != null && _host == Connection?.PeerId;
    private static EnemySync _instance;
    private HeroController _hero;

    protected override float TriggerFrequency => 15.0f;

    private static string CurrentSceneName => SceneManager.GetActiveScene().name;

    public override void Bind(IConnection connection)
    {
        if (_instance)
            throw new InvalidOperationException("Already bound");
        base.Bind(connection);
        _instance = this;
        connection.AddHandler<EnemyPosPacket>(OnEnemyPos);
        connection.AddHandler<EnemyHealthPacket>(OnEnemyHealth);
        connection.AddHandler<EnemyFsmPacket>(OnEnemyFsm);
        connection.AddHandler<AttackRequestPacket>(OnAttackRequest);
        connection.AddHandler<HostPeerPacket>(OnHostPeer);
        connection.Send(new HostPeerPacket());
    }

    protected override void Update()
    {
        base.Update();
        if (!_hero)
            _hero = FindObjectOfType<HeroController>();
    }

    public override void Unbind()
    {
        base.Unbind();
        _instance = null;
    }

    protected override void FixedTrigger()
    {
        if (!IsHost) return;
        foreach (var enemyHm in HealthManager.EnumerateActiveEnemies().Where(hm => hm))
        {
            BroadcastEnemyPos(enemyHm.gameObject);
        }
    }

    private static void OnEnemyPos(EnemyPosPacket packet)
    {
        var enemy = EnemyRegistry.FindById(packet.Id, packet.Scene);
        if (!enemy)
        {
            return;
        }

        if (!enemy.TryGetComponent<LockTransformPosition>(out var transformPosition))
            transformPosition = enemy.AddComponent<LockTransformPosition>();
        transformPosition.UpdateLockedPos(packet.Pos);
    }

    private static void OnEnemyHealth(EnemyHealthPacket packet)
    {
        var enemy = EnemyRegistry.FindById(packet.Id, packet.Scene);
        if (!enemy) return;
        var hm = enemy.GetComponent<HealthManager>();
        if (!hm || enemy.GetComponent<SyncDeadMarker>())
            return;
        if (packet.IsDead)
        {
            if (hm.GetIsDead()) return;
            hm.Die(null, AttackTypes.Generic, true);
            enemy.SetActive(false);
            enemy.AddComponent<SyncDeadMarker>();
        }
        else
        {
            if (!hm.GetIsDead())
                hm.hp = packet.Hp;
        }
    }

    private void OnEnemyFsm(EnemyFsmPacket packet)
    {
        var enemy = EnemyRegistry.FindById(packet.Id, packet.Scene);
        if (!enemy) return;
        if (!enemy.TryGetComponent<PlayMakerFSM>(out var enemyFsm))
            return;
        if (!IsHost)
        {
            enemyFsm.enabled = true;
        }

        if (enemyFsm.Fsm.ActiveStateName == packet.StateName)
            return;
        enemyFsm.SetState(packet.StateName);
    }

    /// <summary>
    /// 非 host 向 host 发送攻击请求, host 响应 enemy health.
    /// </summary>
    private void OnAttackRequest(AttackRequestPacket packet)
    {
        if (!IsHost) return;
        var enemy = EnemyRegistry.FindById(packet.enemyId, packet.scene);
        if (!enemy) return;
        var hm = enemy.GetComponent<HealthManager>();
        if (!hm || !hm.isActiveAndEnabled)
            return;
        hm.Hit(new HitInstance
        {
            Source = _hero ? _hero.gameObject : null,
            AttackType = (AttackTypes)packet.hit.attackType,
            NailElement = (NailElements)packet.hit.nailElement,
            DamageDealt = packet.hit.damageDealt,
            Direction = packet.hit.direction,
            MagnitudeMultiplier = packet.hit.magnitudeMult,
            NonLethal = packet.hit.nonLethal,
            CriticalHit = packet.hit.critical,
            CanWeakHit = packet.hit.canWeakHit,
            Multiplier = packet.hit.multiplier,
            DamageScalingLevel = packet.hit.damageScalingLevel,
            SpecialType = (SpecialTypes)packet.hit.specialType,
            IsHeroDamage = packet.hit.isHeroDamage
        });
        BroadcastEnemyHealth(hm);
    }

    private void OnHostPeer(HostPeerPacket packet)
    {
        _host = packet.Host;
        Utils.Logger?.LogInfo(IsHost
            ? $"You({Connection?.PeerId}) are the host now."
            : $"Host is ({_host}), you({Connection?.PeerId})");
    }

    public void BroadcastEnemyPos(GameObject go)
    {
        if (!go || !go.GetComponent<HealthManager>())
            return;
        var facingLeft = go.transform.localScale.x <= 0;

        var component = go.GetComponent<tk2dSpriteAnimator>();
        var clip = "Idle";
        if (component && component.CurrentClip?.name != null)
            clip = component.CurrentClip.name;
        var packet = new EnemyPosPacket
        {
            Id = EnemyRegistry.GetOrAssignId(go)!,
            Pos = go.transform.position,
            Clip = clip,
            Scene = CurrentSceneName,
            FacingLeft = facingLeft
        };
        Connection?.Send(packet);
    }

    private void BroadcastEnemyHealth(HealthManager hm)
    {
        var id = EnemyRegistry.GetOrAssignId(hm.gameObject);
        if (string.IsNullOrEmpty(id))
            return;
        var packet = new EnemyHealthPacket
        {
            Id = id,
            Hp = hm.hp,
            IsDead = hm.GetIsDead(),
            Scene = CurrentSceneName
        };
        Connection?.Send(packet);
    }

    [HarmonyPatch(typeof(Fsm), "DoTransition")]
    public static class Patch_Fsm_DoTransition
    {
        // ReSharper disable once InconsistentNaming
        private static void Postfix(Fsm __instance, FsmTransition? transition)
        {
            if (!_instance) return;
            if (!_instance.IsHost) return;
            var gameObject = __instance.GameObject;
            if (!gameObject || !gameObject.GetComponent<HealthManager>())
                return;
            var playMakerFsm = gameObject.GetComponents<PlayMakerFSM>()
                .FirstOrDefault(component => component && component.Fsm == __instance);
            if (!playMakerFsm) return;
            var id = EnemyRegistry.GetOrAssignId(gameObject);
            if (string.IsNullOrEmpty(id))
                return;
            var activeScene = SceneManager.GetActiveScene();
            var name = activeScene.name;
            var packet = new EnemyFsmPacket
            {
                Id = id,
                Scene = name,
                StateName = transition?.ToState ?? __instance.ActiveStateName
            };
            if (_instance)
                _instance.Connection?.Send(packet);
        }
    }

    [HarmonyPatch(typeof(HealthManager), "Hit")]
    public static class Patch_HealthManager_Hit
    {
        // ReSharper disable once InconsistentNaming
        private static void Prefix(HealthManager __instance, ref HitInstance hitInstance)
        {
            Utils.Logger?.LogDebug("test Hit Prefix"); // todo del
            if (!_instance) return;
            if (_instance.IsHost) return; // 只有非 host 才发送攻击请求.
            var enemyId = EnemyRegistry.GetOrAssignId(__instance.gameObject);
            if (string.IsNullOrEmpty(enemyId))
                return;
            var attackRequest = new AttackRequestPacket
            {
                enemyId = enemyId,
                scene = SceneManager.GetActiveScene().name,
                hit = new AttackRequestPacket.SimpleHit
                {
                    damageDealt = hitInstance.DamageDealt,
                    direction = hitInstance.Direction,
                    magnitudeMult = hitInstance.MagnitudeMultiplier,
                    attackType = (int)hitInstance.AttackType,
                    nailElement = (int)hitInstance.NailElement,
                    nonLethal = false,
                    critical = hitInstance.CriticalHit,
                    canWeakHit = hitInstance.CanWeakHit,
                    multiplier = hitInstance.Multiplier,
                    damageScalingLevel = hitInstance.DamageScalingLevel,
                    specialType = (int)hitInstance.SpecialType,
                    isHeroDamage = true
                }
            };
            Utils.Logger?.LogDebug("Try sending attack request packet...");
            _instance.Connection?.Send(attackRequest);
        }

        // ReSharper disable once InconsistentNaming
        private static void Postfix(HealthManager __instance)
        {
            Utils.Logger?.LogDebug("test Hit Postfix"); // todo del
            if (!_instance) return;
            if (!_instance.IsHost) return; // host 攻击然后同步小怪血量给其他 peer.
            var enemyId = EnemyRegistry.GetOrAssignId(__instance.gameObject);
            if (string.IsNullOrEmpty(enemyId))
                return;
            var packet = new EnemyHealthPacket
            {
                Id = enemyId,
                Hp = __instance.hp,
                IsDead = __instance.GetIsDead(),
                Scene = SceneManager.GetActiveScene().name
            };
            if (!_instance) return;
            Utils.Logger?.LogDebug("Try sending enemy health packet...");
            _instance.Connection?.Send(packet);
        }
    }
}
