// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Aggregation;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Evaluators.Speed;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    /// <summary>
    /// Represents the skill required to press keys with regards to keeping up with the speed at which objects need to be hit.
    /// </summary>
    public class Speed : Skill
    {
        private class StaminaObject
        {
            public readonly double DifficultyReciprocal;
            public double Count;

            public StaminaObject(double deltaTime)
            {
                DifficultyReciprocal = deltaTime;
                Count = 1;
            }
        }

        private readonly List<double> sliderStrains = new List<double>();
        private readonly List<StaminaObject> staminaMemory = new List<StaminaObject>();

        private double harmonicWeightSum;

        public Speed(Mod[] mods)
            : base(mods)
        {
        }

        protected override double ProcessInternal(DifficultyHitObject current)
        {
            const double stamina_multiplier = 12.4;

            if (Mods.Any(m => m is OsuModRelax))
                return 0;

            double currentDifficulty = calculateAdjustedDifficulty(current);
            double currentRhythm = RhythmEvaluator.EvaluateDifficultyOf(current);

            addStaminaMemory(currentDifficulty);

            double currentStamina = calculateStaminaStrain() * stamina_multiplier;

            double totalStrain = currentRhythm * currentStamina;

            if (current.BaseObject is Slider)
                sliderStrains.Add(totalStrain);

            return totalStrain;
        }

        private double calculateAdjustedDifficulty(DifficultyHitObject current)
        {
            double difficulty = SpeedEvaluator.EvaluateDifficultyOf(current);

            if (Mods.Any(m => m is OsuModAutopilot))
                difficulty *= 0.5;

            return difficulty;
        }

        private void addStaminaMemory(double difficulty)
        {
            StaminaObject curr = new StaminaObject(1.0 / difficulty);

            if (staminaMemory.LastOrDefault() is StaminaObject last && Math.Max(curr.DifficultyReciprocal, last.DifficultyReciprocal) <= 1.1 * Math.Min(curr.DifficultyReciprocal, last.DifficultyReciprocal))
            {
                last.Count++;
            }
            else
            {
                staminaMemory.Add(curr);
            }
        }

        // Useful links for stamina strain calc
        // https://www.wolframalpha.com/input?i2d=true&i=Sum%5B%5C%2840%29Divide%5B1%2C%5C%2840%29k*t%2Ba%5C%2841%29%5D%5C%2841%29%2C%7Bk%2C1%2Cx%7D%5D
        private double calculateStaminaStrain()
        {
            if (staminaMemory.Count == 0) return 0;

            const double count_scalar = 1; // Used to horizontally stretch/squish the final result
            const double stop_factor = 10_000;

            double currentStamina = 0;
            double tSum = 0;

            for (int i = staminaMemory.Count - 1; i >= 0; i--)
            {
                StaminaObject obj = staminaMemory[i];

                double t = obj.DifficultyReciprocal;
                double x = obj.Count * count_scalar;

                currentStamina += (
                    DiffUtils.DiGamma(tSum / t + x + 1.0) -
                    DiffUtils.DiGamma(tSum / t + 1.0)
                ) / t;
                tSum += t * x;

                //
                // The function above produces identical values to this:
                //
                // for (int j = 0; j < x; j++)
                // {
                //     tSum += t;
                //     currentStamina += 1 / tSum;
                // }
                //

                if (currentStamina > 1.0 / tSum * stop_factor)
                    break;
            }

            return currentStamina;
        }

        public override double DifficultyValue()
        {
            if (ObjectDifficulties.Count == 0)
                return 0;

            (double difficulty, harmonicWeightSum) = HarmonicSeries.Aggregate(ObjectDifficulties, harmonicScale: 20);

            return difficulty;
        }

        public double RelevantObjectCount()
        {
            if (ObjectDifficulties.Count == 0)
                return 0;

            double maxStrain = ObjectDifficulties.Max();

            if (maxStrain == 0)
                return 0;

            return ObjectDifficulties.Sum(strain => DiffUtils.Logistic(strain / maxStrain, 0.62, 12.0));
        }

        public virtual double CountTopWeightedObjectDifficulties(double difficultyValue)
        {
            if (ObjectDifficulties.Count == 0)
                return 0.0;

            if (harmonicWeightSum == 0)
                return 0.0;

            double consistentTopObject = difficultyValue / harmonicWeightSum; // What would the top difficulty be if all object difficulties were identical

            if (consistentTopObject == 0)
                return 0;

            return ObjectDifficulties.Sum(d => DiffUtils.Logistic(d / consistentTopObject, 0.88, 10, 1.1));
        }

        public double CountTopWeightedSliders(double difficultyValue)
        {
            if (sliderStrains.Count == 0)
                return 0;

            if (harmonicWeightSum == 0)
                return 0.0;

            double consistentTopObject = difficultyValue / harmonicWeightSum; // What would the top note be if all note values were identical

            if (consistentTopObject == 0)
                return 0;

            // Use a weighted sum of all notes. Constants are arbitrary and give nice values
            return sliderStrains.Sum(s => DiffUtils.Logistic(s / consistentTopObject, 0.88, 10, 1.1));
        }
    }
}
