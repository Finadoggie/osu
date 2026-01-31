// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Evaluators;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    public class FingerControl : HarmonicSkill
    {
        public FingerControl(Mod[] mods)
            : base(mods)
        {
        }

        private double skillMultiplier => 1.3;

        private double currentStrain;

        private readonly List<double> noteHistory = new List<double>();

        public int HardStrains { get; private set; }

        private double strainDecay(double ms) => Math.Pow(Math.Pow(0.75, 1 / Math.Min(ms / 1000, 0.15)), ms / 1000);

        /// <summary>
        /// Calculates finger control difficulty of the map
        /// </summary>
        protected override double ObjectDifficultyOf(DifficultyHitObject current)
        {
            noteHistory.Add(((OsuDifficultyHitObject)current).AdjustedDeltaTime);
            while (noteHistory.Sum() > 4000 || noteHistory.Count > 32)
                noteHistory.RemoveAt(0);

            double decay = strainDecay(((OsuDifficultyHitObject)current).AdjustedDeltaTime);

            currentStrain *= decay;
            currentStrain += FingerControlEvaluator.EvaluateDifficultyOf(current, noteHistory) * skillMultiplier;

            if (currentStrain > 1.1) HardStrains++;

            return currentStrain;
        }

        public override double DifficultyValue()
        {
            const double decay_weight = 0.98;

            double difficulty = 0;
            double weight = 1;

            // Difficulty is the weighted sum of the highest strains from every section.
            // We're sorting from highest to lowest strain.
            foreach (double strain in ObjectDifficulties.OrderDescending())
            {
                difficulty += strain * weight;
                weight *= decay_weight;
            }

            return difficulty * (1 - decay_weight);
        }
    }
}
