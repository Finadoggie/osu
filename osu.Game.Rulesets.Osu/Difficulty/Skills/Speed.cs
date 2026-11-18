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
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
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
            public double difficulty;
            public double count;

            // private double clockAdjustment(double delta)
            // {
            //     // decay tuning parameter. Higher tau = less strain carried from one stream to the next.
            //     // Increasing tau makes the strain graph spikier, and decreasing makes it smoother.
            //     const double tau = 200;
            //     // gap penalisation constant. higher q = stronger nerf for gaps
            //     const double q = 1.5;
            //     return delta * DiffUtils.Pow(1 + delta * tau, q - 1);
            // }

            public StaminaObject(double difficulty)
            {
                this.difficulty = difficulty;
                this.count = 1;
            }
        }

        private readonly List<double> sliderStrains = new List<double>();
        private readonly List<StaminaObject> staminaMemory = new List<StaminaObject>();

        private double currentStrain;
        private double currentBurst;
        private double harmonicWeightSum;

        public Speed(Mod[] mods)
            : base(mods)
        {
        }

        private double calculateStrainDecay(double ms) => DiffUtils.Pow(0.3, ms / 1000);
        private double calculateBurstDecay(double ms) => DiffUtils.Pow(0.01, ms / 1000);

        protected override double ProcessInternal(DifficultyHitObject current)
        {
            const double speed_multiplier = 18.5;
            const double burst_multiplier = 20.27;
            const double stamina_multiplier = 0.015;

            const double strain_stamina_norm = 2;

            const double final_multiplier = 1.5;

            if (Mods.Any(m => m is OsuModRelax))
                return 0;

            double currentDifficulty = calculateAdjustedDifficulty(current);
            double currentStaminaDifficulty = calculateAdjustedStaminaDifficulty(current);
            double currentRhythm = RhythmEvaluator.EvaluateDifficultyOf(current);

            double strainDecay = calculateStrainDecay(((OsuDifficultyHitObject)current).AdjustedDeltaTime);
            double burstDecay = calculateBurstDecay(((OsuDifficultyHitObject)current).AdjustedDeltaTime);

            currentStrain *= strainDecay;
            currentStrain += currentDifficulty * (1 - strainDecay) * speed_multiplier;

            currentBurst *= burstDecay;
            currentBurst += currentDifficulty * (1 - burstDecay) * burst_multiplier;

            StaminaObject currStaminaObj = new StaminaObject(currentStaminaDifficulty);

            if (staminaMemory.Count > 0 && Math.Abs(currStaminaObj.difficulty - staminaMemory.Last().difficulty) < 0.005)
            {
                staminaMemory.Last().count++;
            }
            else
            {
                staminaMemory.Add(currStaminaObj);
            }

            double currentStamina = calculateStaminaStrain() * stamina_multiplier;

            double totalStrain = (DiffUtils.Norm(strain_stamina_norm, currentStrain, currentStamina) + currentBurst) * currentRhythm * final_multiplier;
            // double totalStrain = currentRhythm * (currentStamina + currentBurst + currentStrain);

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

        private double calculateAdjustedStaminaDifficulty(DifficultyHitObject current)
        {
            double difficulty = StaminaEvaluator.EvaluateDifficultyOf(current);

            if (Mods.Any(m => m is OsuModAutopilot))
                difficulty *= 0.5;

            return difficulty;
        }

        // Useful links for stamina strain calc
        // https://www.wolframalpha.com/input?i2d=true&i=Sum%5B%5C%2840%29Divide%5B1%2CPower%5Bt%2C2%5D%5D*Divide%5B1%2C%5C%2840%29kt%2B1%5C%2841%29%5D%5C%2841%29%2C%7Bk%2C0%2Cx%7D%5D
        // https://www.wolframalpha.com/input?i2d=true&i=Sum%5B%5C%2840%29Divide%5B1%2CPower%5Bt%2C2%5D%5C%2840%29kt%2Ba%2B1%5C%2841%29%5D%5C%2841%29%2C%7Bk%2C0%2Cx%7D%5D
        private double calculateStaminaStrain()
        {
            if (staminaMemory.Count == 0) return 0;

            const double lerp_percent = 0.01;

            double currentStamina = 0;
            double currentSumTime = 0; // Offset start to make stamina scale differently
            double maxDeltaTime = Math.Sqrt(1 / staminaMemory.Last().difficulty);

            for (int i = staminaMemory.Count - 1; i >= 0; i--)
            {
                StaminaObject obj = staminaMemory[i];
                double deltaTime = Math.Sqrt(1 / obj.difficulty);

                currentStamina += (
                    DiffUtils.DiGamma((currentSumTime + deltaTime + deltaTime * (obj.count - 1) + 1) / deltaTime) -
                    DiffUtils.DiGamma((currentSumTime + 1) / deltaTime)
                ) / DiffUtils.Pow(deltaTime, 3);

                currentSumTime += deltaTime * obj.count;

                maxDeltaTime = Math.Max(deltaTime, maxDeltaTime);
                double assumedNumObjects = DiffUtils.DiGammaInv(
                    currentStamina * DiffUtils.Pow(maxDeltaTime, 3) + DiffUtils.DiGamma(1.0 / maxDeltaTime)
                ) - (1.0 / maxDeltaTime) - 1;

                currentSumTime = Math.Max(currentSumTime, double.Lerp(currentSumTime, assumedNumObjects * maxDeltaTime, lerp_percent));
                if (currentSumTime > 10_000) break;
            }

            return currentStamina;
        }

        public override double DifficultyValue()
        {
            if (ObjectDifficulties.Count == 0)
                return 0;

            (double difficulty, harmonicWeightSum) = HarmonicSeries.Aggregate(ObjectDifficulties, harmonicScale: 60, decayExponent: 1.10);

            return difficulty;
        }

        public double RelevantObjectCount()
        {
            if (ObjectDifficulties.Count == 0)
                return 0;

            double maxStrain = ObjectDifficulties.Max();

            if (maxStrain == 0)
                return 0;

            return ObjectDifficulties.Sum(strain => DiffUtils.Logistic(strain / maxStrain, 0.5, 12.0));
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
