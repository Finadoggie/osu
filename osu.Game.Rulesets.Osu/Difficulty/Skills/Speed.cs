// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Evaluators;
using osu.Game.Rulesets.Osu.Objects;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Difficulty.Utils;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    /// <summary>
    /// Represents the skill required to press keys with regards to keeping up with the speed at which objects need to be hit.
    /// </summary>
    public class Speed : VariableLengthStrainSkill
    {
        private double skillMultiplier => 1.20;

        private readonly List<double> sliderStrains = new List<double>();

        private double currentDifficulty;

        private double strainDecayBase => 0.3;

        public Speed(Mod[] mods)
            : base(mods)
        {
        }

        private double strainDecay(double ms) => Math.Pow(strainDecayBase, ms / 1000);

        protected override double StrainValueAt(DifficultyHitObject current)
        {
            currentDifficulty *= strainDecay(((OsuDifficultyHitObject)current).AdjustedDeltaTime);
            currentDifficulty += SpeedEvaluator.EvaluateDifficultyOf(current, Mods) * skillMultiplier;

            double currentRhythm = RhythmEvaluator.EvaluateDifficultyOf(current);

            double totalDifficulty = currentDifficulty * currentRhythm;

            if (current.BaseObject is Slider)
                sliderStrains.Add(totalDifficulty);

            return totalDifficulty;
        }

        protected override double CalculateInitialStrain(double time, DifficultyHitObject current)
        {
            return 0;
        }

        public override double DifficultyValue()
        {
            double difficulty = 0;

            // Sections with 0 strain are excluded to avoid worst-case time complexity of the following sort (e.g. /b/2351871).
            // These sections will not contribute to the difficulty.
            var peaks = GetCurrentStrainPeaks().Where(p => p.Value > 0);

            List<StrainPeak> strains = peaks.OrderByDescending(p => (p.Value, p.SectionLength)).ToList();

            // Time is measured in units of strains
            double time = 0;

            // Difficulty is a continuous weighted sum of the sorted strains
            // https://www.desmos.com/calculator/lkc1wtryjz
            for (int i = 0; i < strains.Count; i++)
            {
                double weight = this.weight(time + strains[i].SectionLength / MaxSectionLength) - this.weight(time);
                difficulty += strains[i].Value * weight;
                time += strains[i].SectionLength / MaxSectionLength;
            }

            return difficulty;
        }

        public double RelevantNoteCount()
        {
            if (ObjectStrains.Count == 0)
                return 0;

            double maxStrain = ObjectStrains.Max();
            if (maxStrain == 0)
                return 0;

            return ObjectStrains.Sum(strain => 1.0 / (1.0 + Math.Exp(-(strain / maxStrain * 12.0 - 6.0))));
        }

        public double CountTopWeightedSliders(double difficultyValue)
            => OsuStrainUtils.CountTopWeightedSliders(sliderStrains, difficultyValue);

        private double weight(double time) => (Math.Pow(Math.Log(time * 8 + 1), 2.32) * 0.134398398845 + Math.Log(Math.Pow(time * 8, 2.15) + 1) * 0.793690755297) / 2;
}
}
