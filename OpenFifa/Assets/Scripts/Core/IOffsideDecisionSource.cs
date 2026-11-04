using System;

namespace OpenFifa.Core
{
    /// <summary>
    /// Anything that can call offside. The presentation layer listens to this, so it can be
    /// driven by <see cref="OffsideLogic"/> or by another rules engine (e.g. a match Sim)
    /// that emits the same <see cref="OffsideDecision"/>.
    /// </summary>
    public interface IOffsideDecisionSource
    {
        /// <summary>Fired once per offside offence.</summary>
        event Action<OffsideDecision> OnOffence;
    }
}
