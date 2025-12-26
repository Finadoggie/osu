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
using osu.Game.Rulesets.Osu.Difficulty.Utils;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    /// <summary>
    /// Represents the skill required to press keys with regards to keeping up with the speed at which objects need to be hit.
    /// </summary>
    public class Speed : Skill
    {
        private double skillMultiplier => 1.37;

        private readonly List<double> sliderStrains = new List<double>();

        private double currentDifficulty;

        private List<(double Value, double deltaTime, bool isSlider)> notes = new List<(double, double, bool)>();
        private List<double> noteDistances = new List<double>();

        private double strainDecayBase => 0.3;

        private double noteWeightSum;

        public Speed(Mod[] mods)
            : base(mods)
        {
        }

        private double strainDecay(double ms) => Math.Pow(strainDecayBase, ms / 1000);

        public override void Process(DifficultyHitObject current)
        {
            currentDifficulty = (SpeedEvaluator.EvaluateDifficultyOf(current, Mods) + SpeedEvaluator.EvaluateDistanceBonusOf(current, Mods)) * skillMultiplier;

            double currentRhythm = RhythmEvaluator.EvaluateDifficultyOf(current);

            double totalDifficulty = currentDifficulty * currentRhythm;

            notes.Add((totalDifficulty, current.DeltaTime, (current.BaseObject is Slider)));
        }

        protected IEnumerable<VariableLengthStrainSkill.StrainPeak> GetCurrentStrainPeaks()
        {
            List<VariableLengthStrainSkill.StrainPeak> strains = new List<VariableLengthStrainSkill.StrainPeak>();
            sliderStrains.Clear();

            double lastDeltaTime = 0;
            double lastValue = 0;
            double sumValue = 0;
            double sumDeltaTime = 0;
            double numNotes = 0;
            double numSliders = 0;

            int index = 0;
            double currStrain = 0;

            foreach (var note in notes)
            {
                double oldStrain = currStrain;

                currStrain *= strainDecay(note.deltaTime);
                currStrain += note.Value;

                if (currStrain < oldStrain || !(Math.Max(note.deltaTime, lastDeltaTime) < 1.25 * Math.Min(note.deltaTime, lastDeltaTime) && Math.Max(note.Value, lastValue) < 1.25 * Math.Min(note.Value, lastValue)))
                {
                    strains.Add(new VariableLengthStrainSkill.StrainPeak(oldStrain, numNotes));

                    for (int i = 0; i < numSliders; i++)
                        sliderStrains.Add(oldStrain);

                    sumValue = 1;
                    numNotes = 1;
                    numSliders = note.isSlider ? 1 : 0;
                }
                else
                {
                    numNotes++;
                    if (note.isSlider) numSliders++;
                }

                lastDeltaTime = note.deltaTime;
                lastValue = note.Value;
                index++;
            }

            for (int i = 0; i < numSliders; i++)
                sliderStrains.Add(currStrain);

            return strains.Append(new VariableLengthStrainSkill.StrainPeak(currStrain, numNotes));
        }

        public override double DifficultyValue()
        {
            double difficulty = 0;

            // Sections with 0 strain are excluded to avoid worst-case time complexity of the following sort (e.g. /b/2351871).
            // These sections will not contribute to the difficulty.
            var strains = GetCurrentStrainPeaks().Where(p => p.Value > 0).OrderDescending().ToList();

            // Time is measured in units of strains
            double time = 0;

            // Difficulty is a continuous weighted sum of the sorted strains
            // https://www.desmos.com/calculator/lkc1wtryjz
            for (int i = 0; i < strains.Count; i++)
            {
                double weight = this.weight(time + strains[i].SectionLength) - this.weight(time);
                difficulty += strains[i].Value * weight;
                time += strains[i].SectionLength;
            }

            noteWeightSum = this.weight(time);

            return difficulty;
        }

        public double RelevantNoteCount()
        {
            var strains = GetCurrentStrainPeaks().Where(p => p.Value > 0).OrderDescending().ToList();
            if (strains.Count == 0)
                return 0;

            double maxStrain = strains.MaxBy(p => p.Value).Value;
            if (maxStrain == 0)
                return 0;

            return strains.Sum(strain => 1.0 / (1.0 + Math.Exp(-(strain.Value / maxStrain * 12.0 - 6.0))) * strain.SectionLength);
        }

        /// <summary>
        /// Returns the number of relevant objects weighted against the top note.
        /// </summary>
        public double CountTopWeightedNotes(double difficultyValue)
        {
            var strains = GetCurrentStrainPeaks().Where(p => p.Value > 0).OrderDescending().ToList();

            if (strains.Count == 0)
                return 0.0;

            if (noteWeightSum == 0)
                return 0.0;

            double consistentTopNote = difficultyValue / noteWeightSum; // What would the top note be if all note values were identical

            if (consistentTopNote == 0)
                return 0;

            // Use a weighted sum of all notes. Constants are arbitrary and give nice values
            return strains.Sum(s => 1.1 / (1 + Math.Exp(-10 * (s.Value / consistentTopNote - 0.88))) * s.SectionLength);
        }

        public double CountTopWeightedSliders(double difficultyValue)
            => OsuStrainUtils.CountTopWeightedSliders(sliderStrains, difficultyValue);

        private double weight(double time) => (Math.Pow(Math.Log(time + 1), 2.32) * 0.134398398845 + Math.Log(Math.Pow(time, 2.15) + 1) * 0.793690755297) / 2;
}
}
