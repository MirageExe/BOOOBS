// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.DetailExaminable;
using Content.Shared.Forensics.Components;
using Content.Shared.Preferences;
using Content.Trauma.Shared.Genetics.Mutations;
using System.Linq;

namespace Content.Trauma.Shared.Genetics;

/// <summary>
/// Simple API for getting and changing <see cref="UniqueEnzymes"/> for mobs.
/// </summary>
public sealed class UniqueEnzymesSystem : EntitySystem
{
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly MutationSystem _mutation = default!;

    /// <summary>
    /// Change a mob's unique enzymes, if it is mutatable (i.e. no renaming mice and shit).
    /// </summary>
    public void ChangeEnzymes(EntityUid mob, UniqueEnzymes enzymes)
    {
        if (!_mutation.CanMutate(mob))
            return;

        _meta.SetEntityName(mob, enzymes.Name);
        if (enzymes.Prints is {} print && TryComp<FingerprintComponent>(mob, out var prints))
        {
            prints.Fingerprint = print;
            Dirty(mob, prints);
        }

        // Port Trauma Station: humanoid profile API differs in this fork.
    }

    /// <summary>
    /// Get the unique enzymes for a mob.
    /// </summary>
    public UniqueEnzymes GetEnzymes(EntityUid mob)
    {
        return new UniqueEnzymes(
            Name(mob),
            CompOrNull<FingerprintComponent>(mob)?.Fingerprint,
            null,
            null,
            null,
            null
        );
    }
}
