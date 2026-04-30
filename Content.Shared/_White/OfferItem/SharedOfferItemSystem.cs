using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.Player;

namespace Content.Shared.OfferItem;

public abstract partial class SharedOfferItemSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<OfferItemComponent, InteractUsingEvent>(SetInReceiveMode);
        SubscribeLocalEvent<OfferItemComponent, AcceptOfferAlertEvent>(OnAcceptOfferAlert);
        InitializeInteractions();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<OfferItemComponent>();
        while (query.MoveNext(out var uid, out var offer))
        {
            if (offer.Target == null)
                continue;

            if (!Exists(offer.Target.Value))
            {
                UnOffer(uid, offer);
                continue;
            }

            if (_transform.InRange(Transform(uid).Coordinates, Transform(offer.Target.Value).Coordinates, offer.MaxOfferDistance))
                continue;

            UnOffer(uid, offer);
        }
    }

    private void SetInReceiveMode(EntityUid uid, OfferItemComponent component, InteractUsingEvent args)
    {
        if (!TryComp<OfferItemComponent>(args.User, out var offerItem))
            return;

        if (args.User == uid)
            return;

        if (!offerItem.IsInOfferMode)
            return;

        if (component.IsInReceiveMode)
            return;

        if (offerItem.Target != null && offerItem.Target != uid)
            return;

        args.Handled = true;

        component.IsInReceiveMode = true;
        component.Target = args.User;
        Dirty(uid, component);

        offerItem.Target = uid;
        offerItem.IsInOfferMode = false;
        Dirty(args.User, offerItem);

        if (offerItem.Item == null)
            return;

        _popup.PopupEntity(
            Loc.GetString("offer-item-try-give",
                ("item", Identity.Entity(offerItem.Item.Value, EntityManager)),
                ("target", Identity.Entity(uid, EntityManager))),
            args.User,
            args.User);

        _popup.PopupEntity(
            Loc.GetString("offer-item-try-give-target",
                ("user", Identity.Entity(args.User, EntityManager)),
                ("item", Identity.Entity(offerItem.Item.Value, EntityManager))),
            args.User,
            uid);
    }

    private void OnAcceptOfferAlert(EntityUid uid, OfferItemComponent component, AcceptOfferAlertEvent args)
    {
        if (!TryComp<OfferItemComponent>(component.Target, out var offerItem) ||
            !TryComp<HandsComponent>(uid, out var hands) ||
            offerItem.Hand == null)
            return;

        if (offerItem.Item != null)
        {
            if (!_hands.TryPickup(uid, offerItem.Item.Value, handsComp: hands))
            {
                _popup.PopupClient(Loc.GetString("offer-item-full-hand"), uid, uid);
                return;
            }

            _popup.PopupClient(
                Loc.GetString("offer-item-give",
                    ("item", Identity.Entity(offerItem.Item.Value, EntityManager)),
                    ("target", Identity.Entity(uid, EntityManager))),
                component.Target.Value,
                component.Target.Value);

            _popup.PopupEntity(
                Loc.GetString("offer-item-give-other",
                    ("user", Identity.Entity(component.Target.Value, EntityManager)),
                    ("item", Identity.Entity(offerItem.Item.Value, EntityManager)),
                    ("target", Identity.Entity(uid, EntityManager))),
                component.Target.Value,
                Filter.PvsExcept(component.Target.Value, entityManager: EntityManager),
                true);
        }

        offerItem.Item = null;
        UnReceive(uid, component, offerItem);
    }

    protected void UnOffer(EntityUid uid, OfferItemComponent component)
    {
        // Early exit if no target or target component doesn't exist
        if (component.Target == null || !TryComp(component.Target, out OfferItemComponent? offerItem))
        {
            // Just reset self without popups
            component.IsInOfferMode = false;
            component.IsInReceiveMode = false;
            component.Hand = null;
            component.Target = null;
            component.Item = null;
            Dirty(uid, component);
            return;
        }

        // Cache target before resetting to avoid issues with repeated calls
        var targetUid = component.Target.Value;
        var item = component.Item ?? offerItem.Item;

        // Reset states FIRST to prevent duplicate calls from Update loop
        offerItem.IsInOfferMode = false;
        offerItem.IsInReceiveMode = false;
        offerItem.Hand = null;
        offerItem.Target = null;
        offerItem.Item = null;
        Dirty(targetUid, offerItem);

        component.IsInOfferMode = false;
        component.IsInReceiveMode = false;
        component.Hand = null;
        component.Target = null;
        component.Item = null;
        Dirty(uid, component);

        // Show popups AFTER resetting state
        if (item != null)
        {
            if (component.Item != null)
            {
                // This entity was offering the item
                _popup.PopupEntity(
                    Loc.GetString("offer-item-no-give",
                        ("item", Identity.Entity(item.Value, EntityManager)),
                        ("target", Identity.Entity(targetUid, EntityManager))),
                    uid,
                    uid);

                _popup.PopupEntity(
                    Loc.GetString("offer-item-no-give-target",
                        ("user", Identity.Entity(uid, EntityManager)),
                        ("item", Identity.Entity(item.Value, EntityManager))),
                    uid,
                    targetUid);
            }
            else
            {
                // The target was offering the item
                _popup.PopupEntity(
                    Loc.GetString("offer-item-no-give",
                        ("item", Identity.Entity(item.Value, EntityManager)),
                        ("target", Identity.Entity(uid, EntityManager))),
                    targetUid,
                    targetUid);

                _popup.PopupEntity(
                    Loc.GetString("offer-item-no-give-target",
                        ("user", Identity.Entity(targetUid, EntityManager)),
                        ("item", Identity.Entity(item.Value, EntityManager))),
                    targetUid,
                    uid);
            }
        }
    }

    protected void UnReceive(EntityUid uid, OfferItemComponent? component = null, OfferItemComponent? offerItem = null)
    {
        if (component == null && !TryComp(uid, out component))
            return;

        if (offerItem == null && !TryComp(component.Target, out offerItem))
            return;

        if (component.Target == null)
            return;

        var targetUid = component.Target.Value;
        var item = offerItem.Item;

        // Reset states FIRST to prevent duplicate calls
        offerItem.Item = null;
        offerItem.Hand = null;
        offerItem.IsInOfferMode = false;
        component.IsInReceiveMode = false;

        if (!offerItem.IsInReceiveMode)
        {
            offerItem.Target = null;
            component.Target = null;
        }

        Dirty(uid, component);
        Dirty(targetUid, offerItem);

        // Show popups AFTER resetting state
        if (item != null)
        {
            _popup.PopupEntity(
                Loc.GetString("offer-item-no-give",
                    ("item", Identity.Entity(item.Value, EntityManager)),
                    ("target", Identity.Entity(uid, EntityManager))),
                targetUid,
                targetUid);

            _popup.PopupEntity(
                Loc.GetString("offer-item-no-give-target",
                    ("user", Identity.Entity(targetUid, EntityManager)),
                    ("item", Identity.Entity(item.Value, EntityManager))),
                targetUid,
                uid);
        }
    }

    protected bool IsInOfferMode(EntityUid? entity, OfferItemComponent? component = null)
    {
        return entity != null && Resolve(entity.Value, ref component, false) && component.IsInOfferMode;
    }
}
