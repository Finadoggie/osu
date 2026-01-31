// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Evaluators;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    public class FingerControl : Skill
    {
        public FingerControl(Mod[] mods)
            : base(mods)
        {
        }

        protected double DecayWeight => 0.95;

        private double skillMultiplier => 1.1 * 0.05;
        private double strainDecayBase => 0.15;

        private double currentStrain;
        private double repeatStrainCount;

        private double strainDecay(double ms) => Math.Pow(strainDecayBase, 1 / Math.Min(ms / 1000, 0.2));

        /// <summary>
        /// Calculates finger control difficulty of the map
        /// </summary>
        protected override double ProcessInternal(DifficultyHitObject current)
        {
            var osuCurrObj = (OsuDifficultyHitObject)current;
            var osuLastObj = (OsuDifficultyHitObject?)current.Previous(0);

            if (osuLastObj is null)
                repeatStrainCount = 1;
            else if (Math.Abs(osuCurrObj.AdjustedDeltaTime - osuLastObj.AdjustedDeltaTime) > 0.004)
                repeatStrainCount = 1;
            else
                repeatStrainCount++;

            currentStrain *= strainDecay(osuCurrObj.AdjustedDeltaTime);
            currentStrain += FingerControlEvaluator.EvaluateDifficultyOf(current, repeatStrainCount);

            return currentStrain;
        }

        public override double DifficultyValue()
        {
            double difficulty = 0;
            double weight = 1;

            // Difficulty is the weighted sum of the highest strains from every section.
            // We're sorting from highest to lowest strain.
            foreach (double strain in ObjectDifficulties.OrderDescending())
            {
                difficulty += strain * weight;
                weight *= DecayWeight;
            }

            return difficulty * 1.1;
        }
    }
}
