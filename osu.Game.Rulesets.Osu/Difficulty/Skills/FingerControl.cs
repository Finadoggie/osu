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

        private double skillMultiplier => 11.1;

        private double currentStrain;

        private readonly List<double> noteHistory = new List<double>();

        private double strainDecay(double ms) => Math.Pow(Math.Pow(0.75, 1 / Math.Min(ms / 1000, 0.15)), ms / 1000);

        /// <summary>
        /// Calculates finger control difficulty of the map
        /// </summary>
        protected override double ObjectDifficultyOf(DifficultyHitObject current)
        {
            currentStrain *= 0;//strainDecay(((OsuDifficultyHitObject)current).AdjustedDeltaTime);
            currentStrain += StrainValueOf(current) * skillMultiplier;

            return currentStrain;
        }

        protected double StrainValueOf(DifficultyHitObject current)
        {
            var osuCurrObj = (OsuDifficultyHitObject)current;

            noteHistory.Add(osuCurrObj.AdjustedDeltaTime);

            while (noteHistory.Sum() > 4 || noteHistory.Count > 32)
                noteHistory.RemoveAt(0);

            double strain = FingerControlEvaluator.EvaluateDifficultyOf(current, noteHistory);

            return strain;
        }
    }
}
