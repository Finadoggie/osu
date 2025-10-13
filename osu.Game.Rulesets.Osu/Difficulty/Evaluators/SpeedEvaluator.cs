// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Difficulty.Evaluators
{
    public static class SpeedEvaluator
    {
        private const double single_spacing_threshold = OsuDifficultyHitObject.NORMALISED_DIAMETER * 1.25; // 1.25 circles distance between centers
        private const double min_speed_bonus = 200; // 200 BPM 1/4th
        private const double speed_balancing_factor = 40;
        private const double distance_multiplier = 0.96;

        /// <summary>
        /// Evaluates the difficulty of tapping the current object, based on:
        /// <list type="bullet">
        /// <item><description>time between pressing the previous and current object,</description></item>
        /// <item><description>distance between those objects,</description></item>
        /// <item><description>and how easily they can be cheesed.</description></item>
        /// </list>
        /// </summary>
        public static double EvaluateDifficultyOf(DifficultyHitObject current, IReadOnlyList<Mod> mods)
        {
            if (current.BaseObject is Spinner)
                return 0;

            // derive strainTime for calculation
            var osuCurrObj = (OsuDifficultyHitObject)current;
            var osuPrevObj = current.Index > 0 ? (OsuDifficultyHitObject)current.Previous(0) : null;

            double strainTime = osuCurrObj.AdjustedDeltaTime;
            double doubletapness = 1.0 - osuCurrObj.GetDoubletapness((OsuDifficultyHitObject?)osuCurrObj.Next(0));

            // Cap deltatime to the OD 300 hitwindow.
            // 0.93 is derived from making sure 260bpm OD8 streams aren't nerfed harshly, whilst 0.92 limits the effect of the cap.
            strainTime /= Math.Clamp((strainTime / osuCurrObj.HitWindowGreat) / 0.93, 0.92, 1);

            // speedBonus will be 0.0 for BPM < 200
            double speedBonus = 0.0;

            // Add additional scaling bonus for streams/bursts higher than 200bpm
            if (DifficultyCalculationUtils.MillisecondsToBPM(strainTime) > min_speed_bonus)
                speedBonus = 0.75 * Math.Pow((DifficultyCalculationUtils.BPMToMilliseconds(min_speed_bonus) - strainTime) / speed_balancing_factor, 2);

            double travelDistance = osuPrevObj?.TravelDistance ?? 0;
            double distance = travelDistance + osuCurrObj.MinimumJumpDistance;

            // Cap distance at single_spacing_threshold
            distance = Math.Min(distance, single_spacing_threshold);

            // Max distance bonus is 1 * `distance_multiplier` at single_spacing_threshold
            double distanceBonus = Math.Pow(distance / single_spacing_threshold, 3.95) * distance_multiplier;

            // Apply reduced small circle bonus because flow aim difficulty on small circles doesn't scale as hard as jumps
            distanceBonus *= Math.Sqrt(osuCurrObj.SmallCircleBonus);

            if (mods.OfType<OsuModAutopilot>().Any())
                distanceBonus = 0;

            distanceBonus *= calcCircleStreamNerf(osuCurrObj);

            // Base difficulty with all bonuses
            double difficulty = (1 + speedBonus + distanceBonus) * 1000 / strainTime;

            // Apply penalty if there's doubletappable doubles
            return difficulty * doubletapness;
        }

        private static double calcCircleStreamNerf(OsuDifficultyHitObject osuCurrObj)
        {
            if (osuCurrObj.SignedAngle is null || osuCurrObj.Index <= 1) return 1;

            const double base_angle_nerf = 0.8;
            const double taper_off = 0.9;

            double angleNerf = 1 - base_angle_nerf;
            double mult = 1;

            double prevSignedAngle = osuCurrObj.SignedAngle.Value;

            for (int i = 0; i < Math.Min(osuCurrObj.Index, 8); i++)
            {
                OsuDifficultyHitObject objBeingChecked = (OsuDifficultyHitObject)osuCurrObj.Previous(i);
                if (objBeingChecked.Angle is null || objBeingChecked.SignedAngle is null) break;

                double angle = objBeingChecked.Angle.Value;
                double signedAngle = objBeingChecked.SignedAngle.Value;

                // Reduce nerf extra when angles have a high difference since those aren't circles
                angleNerf *= 1 - Math.Min(Math.Pow(Math.Abs(signedAngle - prevSignedAngle) / double.DegreesToRadians(30), 3), 1);

                mult *= 1 - angleNerf * calcAngleNerf(angle);
                // Reduce nerf per object
                angleNerf *= taper_off;

                prevSignedAngle = signedAngle;
            }

            return mult;
        }

        private static double calcAngleNerf(double angle) => DifficultyCalculationUtils.SmoothstepBellCurve(angle, double.DegreesToRadians(150), 30);
    }
}
