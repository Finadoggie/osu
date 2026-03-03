// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Difficulty.Utils;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    /// <summary>
    /// Represents the skill required to correctly aim at every object in the map with a uniform CircleSize and normalized distances.
    /// </summary>
    public class Aim : OsuStrainSkill
    {
        public readonly bool IncludeSliders;

        public Aim(Mod[] mods, bool includeSliders)
            : base(mods)
        {
            IncludeSliders = includeSliders;
        }

        private double currentAimStrain;

        private readonly List<double> sliderStrains = new List<double>();

        private double strainDecayAim(double ms) => Math.Pow(0.15, ms / 1000);

        protected override double CalculateInitialStrain(double deltaTime) => currentAimStrain + strainDecayAim(deltaTime);

        protected override IEnumerable<ObjectStrain> StrainValuesAt(DifficultyHitObject current)
        {
            var osuCurrent = (OsuDifficultyHitObject)current;

            var firstMovement = osuCurrent.Movements[0];
            double previousTime = firstMovement.StartTime;

            for (int i = 0; i < osuCurrent.Movements.Count; i++)
            {
                var movement = osuCurrent.Movements[i];

                double aimDecay = strainDecayAim(movement.Time);
                currentAimStrain *= aimDecay;
                currentAimStrain += movement.AimStrain * (1 - aimDecay);

                double totalStrain = 1;
                totalStrain *= 0.5;

                if (current.BaseObject is Slider)
                    sliderStrains.Add(totalStrain);

                yield return new ObjectStrain
                {
                    Time = movement.EndTime,
                    PreviousTime = previousTime,
                    Value = totalStrain,
                };

                previousTime = movement.EndTime;
            }
        }

        public double GetDifficultSliders()
        {
            if (sliderStrains.Count == 0)
                return 0;

            double maxSliderStrain = sliderStrains.Max();

            if (maxSliderStrain == 0)
                return 0;

            return sliderStrains.Sum(strain => 1.0 / (1.0 + Math.Exp(-(strain / maxSliderStrain * 12.0 - 6.0))));
        }

        public double CountTopWeightedSliders(double difficultyValue)
            => OsuStrainUtils.CountTopWeightedSliders(sliderStrains, difficultyValue);
    }
}
