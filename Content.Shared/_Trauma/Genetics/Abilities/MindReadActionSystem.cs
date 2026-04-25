// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Common.Religion;
using Content.Shared.Chat;
using Content.Shared.CombatMode;
using Content.Shared.IdentityManagement;
using Content.Shared.Mind;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Trauma.Shared.Genetics.Abilities;

public sealed class MindReadActionSystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MobStateSystem _mob = default!;
    [Dependency] private readonly SharedCombatModeSystem _combatMode = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    // Port Trauma Station: this fork doesn't support injecting EntityQuery<T> via DI.
    private EntityQuery<ActorComponent> _actorQuery;

    private List<string> _recent = new();

    public override void Initialize()
    {
        base.Initialize();

        _actorQuery = GetEntityQuery<ActorComponent>();

        SubscribeLocalEvent<MindReadActionComponent, MindReadActionEvent>(OnMindRead);
    }

    private void OnMindRead(Entity<MindReadActionComponent> ent, ref MindReadActionEvent args)
    {
        var user = args.Performer;
        var target = args.Target;

        if (!_actorQuery.TryComp(user, out var actor))
            return;

        var channel = actor.PlayerSession.Channel;

        args.Handled = true;

        // check if they are valid to begin with
        var identity = Identity.Name(target, EntityManager);
        if (!_mind.TryGetMind(target, out var mindId, out var mind))
        {
            _popup.PopupClient(Loc.GetString("MutationMindReader-popup-target-mindless", ("target", identity)), user, user);
            return;
        }

        if (_mob.IsDead(target))
        {
            _popup.PopupClient(Loc.GetString("MutationMindReader-popup-target-dead", ("target", identity)), user, user);
            return;
        }

        // nullrod protects from mind magic idk
        var ev = new BeforeCastTouchSpellEvent(target);
        RaiseLocalEvent(target, ev);
        if (ev.Cancelled)
        {
            _popup.PopupClient(Loc.GetString("MutationMindReader-popup-mind-protected", ("target", identity)), user, user);
            return;
        }

        if (user == target)
        {
            _popup.PopupClient(Loc.GetString("MutationMindReader-popup-self"), user, user);
            return;
        }

        _popup.PopupClient(Loc.GetString("MutationMindReader-popup-plunge", ("target", identity)), user, user);

        // you don't know details about other players' minds.
        // also it's using chatcode anyway
        if (_net.IsClient) return;

        // chance to alert the target
        if (_random.Prob(ent.Comp.AlertProb))
            _popup.PopupEntity(Loc.GetString("MutationMindReader-popup-alert"), target, target, PopupType.MediumCaution);

        // doesn't matter much because of combat mode spinning but parity
        var combat = _combatMode.IsInCombatMode(target);
        Tell(channel, Loc.GetString("MutationMindReader-popup-combat-mode", ("target", target), ("combat", combat)));

        // reveal mindswaps or whatever
        if (mind.CharacterName is {} name && name != identity)
            Tell(channel, Loc.GetString("MutationMindReader-popup-true-identity", ("target", target), ("name", name)), Color.Red);
    }

    private void Tell(INetChannel client, string message, Color? color = null)
    {
        // Port Trauma Station: per-client chat helper differs in this fork.
    }
}
