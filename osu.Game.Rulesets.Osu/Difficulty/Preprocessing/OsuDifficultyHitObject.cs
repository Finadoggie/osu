// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Scoring;
using osuTK;

namespace osu.Game.Rulesets.Osu.Difficulty.Preprocessing
{
    public class OsuDifficultyHitObject : DifficultyHitObject
    {
        /// <summary>
        /// A distance by which all distances should be scaled in order to assume a uniform circle size.
        /// </summary>
        public const int NORMALISED_RADIUS = 50; // Change radius to 50 to make 100 the diameter. Easier for mental maths.

        public const int NORMALISED_DIAMETER = NORMALISED_RADIUS * 2;

        public const int MIN_DELTA_TIME = 25;

        public const float MAXIMUM_SLIDER_RADIUS = NORMALISED_RADIUS * 2.4f;
        public const float ASSUMED_SLIDER_RADIUS = NORMALISED_RADIUS * 1.8f;

        protected new OsuHitObject BaseObject => (OsuHitObject)base.BaseObject;
        protected new OsuHitObject LastObject => (OsuHitObject)base.LastObject;

        /// <summary>
        /// Milliseconds elapsed since the start time of the previous <see cref="OsuDifficultyHitObject"/>, with a minimum of 25ms.
        /// </summary>
        public readonly double StrainTime;

        /// <summary>
        /// Normalised distance from the "lazy" end position of the previous <see cref="OsuDifficultyHitObject"/> to the start position of this <see cref="OsuDifficultyHitObject"/>.
        /// <para>
        /// The "lazy" end position is the position at which the cursor ends up if the previous hitobject is followed with as minimal movement as possible (i.e. on the edge of slider follow circles).
        /// </para>
        /// </summary>
        public double LazyJumpDistance { get; private set; }

        /// <summary>
        /// Normalised shortest distance to consider for a jump between the previous <see cref="OsuDifficultyHitObject"/> and this <see cref="OsuDifficultyHitObject"/>.
        /// </summary>
        /// <remarks>
        /// This is bounded from above by <see cref="LazyJumpDistance"/>, and is smaller than the former if a more natural path is able to be taken through the previous <see cref="OsuDifficultyHitObject"/>.
        /// </remarks>
        /// <example>
        /// Suppose a linear slider - circle pattern.
        /// <br />
        /// Following the slider lazily (see: <see cref="LazyJumpDistance"/>) will result in underestimating the true end position of the slider as being closer towards the start position.
        /// As a result, <see cref="LazyJumpDistance"/> overestimates the jump distance because the player is able to take a more natural path by following through the slider to its end,
        /// such that the jump is felt as only starting from the slider's true end position.
        /// <br />
        /// Now consider a slider - circle pattern where the circle is stacked along the path inside the slider.
        /// In this case, the lazy end position correctly estimates the true end position of the slider and provides the more natural movement path.
        /// </example>
        public double MinimumJumpDistance { get; private set; }

        /// <summary>
        /// The time taken to travel through <see cref="MinimumJumpDistance"/>, with a minimum value of 25ms.
        /// </summary>
        public double MinimumJumpTime { get; private set; }

        /// <summary>
        /// The time taken to travel through <see cref="MinimumJumpDistance"/>, with a minimum value of 25ms.
        /// </summary>
        public double? SliderlessJumpDistance { get; private set; }

        /// <summary>
        /// The position of the cursor at the point of completion of this <see cref="OsuDifficultyHitObject"/> if it is a <see cref="Slider"/>
        /// and was hit with as few movements as possible.
        /// </summary>
        public Vector2 CursorPosition { get; private set; }

        /// <summary>
        /// Angle the player has to take to hit this <see cref="OsuDifficultyHitObject"/>.
        /// Calculated as something.
        /// </summary>
        public double? Angle { get; private set; }

        /// <summary>
        /// Angle the player has to take to hit this <see cref="OsuDifficultyHitObject"/>.
        /// Calculated as the angle between the circles (current-2, current-1, current).
        /// </summary>
        public double? SliderlessAngle { get; private set; }

        /// <summary>
        /// Retrieves the full hit window for a Great <see cref="HitResult"/>.
        /// </summary>
        public double HitWindowGreat { get; private set; }

        /// <summary>
        /// Selective bonus for maps with higher circle size.
        /// </summary>
        public double SmallCircleBonus { get; private set; }

        // /// <summary>
        // /// Area of the intersection between this object and the next, represented in units of Circle Size.
        // /// </summary>
        // public double? OverlapCS { get; private set; }
        //
        // /// <summary>
        // /// Selective bonus for certain types of overlaps.
        // /// </summary>
        // public double? OverlapBonus { get; private set; }

        /// <summary>
        /// Returns true if the <see cref="DifficultyHitObject"/> requires a tap (is a circle or slider head)
        /// </summary>
        public bool IsTapObject { get; private set; }

        /// <summary>
        /// Milliseconds elapsed since the start time of the previous <see cref="OsuDifficultyHitObject"/> satisfying <see cref="IsTapObject"/>, with a minimum of 25ms.
        /// </summary>
        public readonly double TapStrainTime;

        /// <summary>
        /// The index of this <see cref="DifficultyHitObject"/> in the list of all <see cref="DifficultyHitObject"/>s satisfying <see cref="IsTapObject"/>.
        /// Is null if this object is not a circle or slider head.
        /// </summary>
        public int? TapIndex;

        private readonly IReadOnlyList<DifficultyHitObject>? difficultyTapHitObjects;

        private readonly OsuDifficultyHitObject? lastLastDifficultyObject;
        private readonly OsuDifficultyHitObject? lastDifficultyObject;
        private readonly OsuDifficultyHitObject? lastLastTapDifficultyObject;
        private readonly OsuDifficultyHitObject? lastTapDifficultyObject;

        public OsuDifficultyHitObject(HitObject hitObject, HitObject lastObject, double clockRate, List<DifficultyHitObject> objects, int index, List<DifficultyHitObject>? tapObjects = null, int? tapIndex = null, OsuHitObject? parent = null)
            : base(hitObject, lastObject, clockRate, objects, index)
        {
            lastLastDifficultyObject = index > 1 ? (OsuDifficultyHitObject)Previous(1) : null;
            lastDifficultyObject = index > 0 ? (OsuDifficultyHitObject)Previous(0) : null;

            // Capped to 25ms to prevent difficulty calculation breaking from simultaneous objects.
            StrainTime = Math.Max(DeltaTime, MIN_DELTA_TIME);
            TapStrainTime = StrainTime;
            MinimumJumpTime = Math.Max(StrainTime, MIN_DELTA_TIME);

            SmallCircleBonus = Math.Max(1.0, 1.0 + (30 - BaseObject.Radius) / 40);

            if (BaseObject is Slider sliderObject)
            {
                HitWindowGreat = 2 * sliderObject.HeadCircle.HitWindows.WindowFor(HitResult.Great) / clockRate;
            }
            else
            {
                HitWindowGreat = 2 * BaseObject.HitWindows.WindowFor(HitResult.Great) / clockRate;
            }

            if (tapObjects is not null && tapIndex is not null)
            {
                difficultyTapHitObjects = tapObjects;
                TapIndex = tapIndex;
                IsTapObject = true;

                lastLastTapDifficultyObject = tapIndex > 1 ? (OsuDifficultyHitObject)PreviousTap(1) : null;
                lastTapDifficultyObject = tapIndex > 0 ? (OsuDifficultyHitObject)PreviousTap(0) : null;

                OsuDifficultyHitObject? lastDifficultyTapObject = tapIndex > 0 ? (OsuDifficultyHitObject)tapObjects[(int)tapIndex - 1] : null;
                if (lastDifficultyTapObject is not null)
                    TapStrainTime = Math.Max(StartTime - lastDifficultyTapObject.StartTime, MIN_DELTA_TIME);
            }
            else
                IsTapObject = false;

            if ((tapObjects is not null && tapIndex is null) || (tapObjects is null && tapIndex is not null))
                throw new MissingFieldException("tapObjects or tapIndex is not assigned during construction.");

            if (parent is Slider slider)
                calculateCursorPosition(slider);
            else
                calculateCursorPosition();

            setDistances(clockRate);

            // lastDifficultyObject?.calculateOverlapWithNext(this);
        }

        public double OpacityAt(double time, bool hidden)
        {
            if (time > BaseObject.StartTime)
            {
                // Consider a hitobject as being invisible when its start time is passed.
                // In reality the hitobject will be visible beyond its start time up until its hittable window has passed,
                // but this is an approximation and such a case is unlikely to be hit where this function is used.
                return 0.0;
            }

            double fadeInStartTime = BaseObject.StartTime - BaseObject.TimePreempt;
            double fadeInDuration = BaseObject.TimeFadeIn;

            // TODO: Fix this later
            if (fadeInDuration == 0)
            {
                fadeInDuration = LastObject.TimeFadeIn;
                BaseObject.TimeFadeIn = LastObject.TimeFadeIn;
            }

            if (hidden)
            {
                // Taken from OsuModHidden.
                double fadeOutStartTime = BaseObject.StartTime - BaseObject.TimePreempt + BaseObject.TimeFadeIn;
                double fadeOutDuration = BaseObject.TimePreempt * OsuModHidden.FADE_OUT_DURATION_MULTIPLIER;

                return Math.Min
                (
                    Math.Clamp((time - fadeInStartTime) / fadeInDuration, 0.0, 1.0),
                    1.0 - Math.Clamp((time - fadeOutStartTime) / fadeOutDuration, 0.0, 1.0)
                );
            }

            return Math.Clamp((time - fadeInStartTime) / fadeInDuration, 0.0, 1.0);
        }

        /// <summary>
        /// Returns how possible is it to doubletap this object together with the next one and get perfect judgement in range from 0 to 1
        /// </summary>
        public double GetDoubletapness(OsuDifficultyHitObject? osuNextObj)
        {
            if (osuNextObj != null)
            {
                double currDeltaTime = Math.Max(1, DeltaTime);
                double nextDeltaTime = Math.Max(1, osuNextObj.DeltaTime);
                double deltaDifference = Math.Abs(nextDeltaTime - currDeltaTime);
                double speedRatio = currDeltaTime / Math.Max(currDeltaTime, deltaDifference);
                double windowRatio = Math.Pow(Math.Min(1, currDeltaTime / HitWindowGreat), 2);
                return 1.0 - Math.Pow(speedRatio, 1 - windowRatio);
            }

            return 0;
        }

        private void setDistances(double clockRate)
        {
            // if (BaseObject is Slider currentSlider)
            // {
            //     // Bonus for repeat sliders until a better per nested object strain system can be achieved.
            //     TravelDistance = LazyTravelDistance * Math.Pow(1 + currentSlider.RepeatCount / 2.5, 1.0 / 2.5);
            //     TravelTime = Math.Max(LazyTravelTime / clockRate, MIN_DELTA_TIME);
            // }

            // We don't need to calculate either angle or distance when one of the last->curr objects is a spinner
            if (BaseObject is Spinner || LastObject is Spinner)
                return;

            // We will scale distances by this factor, so we can assume a uniform CircleSize among beatmaps.
            float scalingFactor = NORMALISED_RADIUS / (float)BaseObject.Radius;

            Vector2 currCursorPosition = CursorPosition;
            Vector2 lastCursorPosition = lastDifficultyObject?.CursorPosition ?? LastObject.StackedPosition;

            LazyJumpDistance = Vector2.Subtract(currCursorPosition, lastCursorPosition).Length * scalingFactor;
            MinimumJumpTime = Math.Max(StrainTime, MIN_DELTA_TIME);
            MinimumJumpDistance = LazyJumpDistance;

            if (lastTapDifficultyObject is not null)
            {
                SliderlessJumpDistance = Vector2.Subtract(BaseObject.StackedPosition, lastTapDifficultyObject.BaseObject.StackedPosition).Length * scalingFactor;

                if (lastLastTapDifficultyObject != null && lastLastTapDifficultyObject.BaseObject is not Spinner)
                {
                    // // Calculates angle based on actual object positions
                    Vector2 v1 = lastLastTapDifficultyObject.BaseObject.StackedPosition - lastTapDifficultyObject.BaseObject.StackedPosition;
                    Vector2 v2 = BaseObject.StackedPosition - lastTapDifficultyObject.BaseObject.StackedPosition;

                    OsuDifficultyHitObject prevObj = lastLastTapDifficultyObject;
                    OsuDifficultyHitObject prevPrevObj = (OsuDifficultyHitObject)lastLastTapDifficultyObject.PreviousTap(0);

                    // If the current cursor pos is close enough to the previous one
                    // Ignore the angle from it and recalc the angle from earlier objects
                    while (v2.Length * scalingFactor < 20 && prevObj is not null && prevPrevObj is not null)
                    {
                        v1 = prevPrevObj.BaseObject.StackedPosition - prevObj.BaseObject.StackedPosition;
                        v2 = BaseObject.StackedPosition - prevObj.BaseObject.StackedPosition;

                        if (v2.Length * scalingFactor < 20)
                        {
                            prevObj = (OsuDifficultyHitObject)prevObj.PreviousTap(0);
                            prevPrevObj = (OsuDifficultyHitObject)prevPrevObj.PreviousTap(0);
                        }
                    }

                    while (v1.Length * scalingFactor < 20 && prevObj is not null && prevPrevObj is not null)
                    {
                        v1 = prevPrevObj.BaseObject.StackedPosition - prevObj.BaseObject.StackedPosition;
                        prevPrevObj = (OsuDifficultyHitObject)prevPrevObj.PreviousTap(0);
                    }

                    float dot = Vector2.Dot(v1, v2);
                    float det = v1.X * v2.Y - v1.Y * v2.X;

                    SliderlessAngle = Math.Abs(Math.Atan2(det, dot));
                }
            }

            if (LastObject is SliderTailCircle lastSliderCircle)
            {
                // Account for 32ms sliderend leniency
                MinimumJumpTime = Math.Max(StrainTime - SliderEventGenerator.TAIL_LENIENCY, MIN_DELTA_TIME);

                //
                // There are two types of slider-to-object patterns to consider in order to better approximate the real movement a player will take to jump between the hitobjects.
                //
                // 1. The anti-flow pattern, where players cut the slider short in order to move to the next hitobject.
                //
                //      <======o==>  ← slider
                //             |     ← most natural jump path
                //             o     ← a follow-up hitcircle
                //
                // In this case the most natural jump path is approximated by LazyJumpDistance.
                //
                // 2. The flow pattern, where players follow through the slider to its visual extent into the next hitobject.
                //
                //      <======o==>---o
                //                  ↑
                //        most natural jump path
                //
                // In this case the most natural jump path is better approximated by a new distance called "tailJumpDistance" - the distance between the slider's tail and the next hitobject.
                //
                // Thus, the player is assumed to jump the minimum of these two distances in all cases.
                //
                float tailJumpDistance = Vector2.Subtract(lastSliderCircle.StackedPosition, currCursorPosition).Length * scalingFactor;
                MinimumJumpDistance = Math.Max(0, Math.Min(LazyJumpDistance - (MAXIMUM_SLIDER_RADIUS - ASSUMED_SLIDER_RADIUS), tailJumpDistance - MAXIMUM_SLIDER_RADIUS));
            }

            if (lastLastDifficultyObject != null && lastLastDifficultyObject.BaseObject is not Spinner)
            {
                // // Calculates angle based on cursor positions
                // Vector2 lastLastCursorPosition = GetEndCursorPosition(lastLastDifficultyObject);
                //
                // Vector2 v1 = lastLastCursorPosition - lastCursorPosition;
                // Vector2 v2 = currCursorPosition - lastCursorPosition;

                // // Calculates angle based on actual object positions
                Vector2 v1 = lastLastDifficultyObject.CursorPosition - lastCursorPosition;
                Vector2 v2 = currCursorPosition - lastCursorPosition;

                OsuDifficultyHitObject prevObj = lastLastDifficultyObject;
                OsuDifficultyHitObject prevPrevObj = (OsuDifficultyHitObject)lastLastDifficultyObject.Previous(0);

                // If the current cursor pos is close enough to the previous one
                // Ignore the angle from it and recalc the angle from earlier objects
                while (v2.Length * scalingFactor < 20 && prevObj is not null && prevPrevObj is not null)
                {
                    v1 = prevPrevObj.CursorPosition - prevObj.CursorPosition;
                    v2 = currCursorPosition - prevObj.CursorPosition;

                    if (v2.Length * scalingFactor < 20)
                    {
                        prevObj = (OsuDifficultyHitObject)prevObj.Previous(0);
                        prevPrevObj = (OsuDifficultyHitObject)prevPrevObj.Previous(0);
                    }
                }

                while (v1.Length * scalingFactor < 20 && prevObj is not null && prevPrevObj is not null)
                {
                    v1 = prevPrevObj.CursorPosition - prevObj.CursorPosition;
                    prevPrevObj = (OsuDifficultyHitObject)prevPrevObj.Previous(0);
                }

                float dot = Vector2.Dot(v1, v2);
                float det = v1.X * v2.Y - v1.Y * v2.X;

                Angle = Math.Abs(Math.Atan2(det, dot));
            }
        }

        private void calculateCursorPosition(Slider? slider = null)
        {
            if (Index == 0 || IsTapObject || slider is null)
            {
                CursorPosition = BaseObject.StackedPosition;
                return;
            }

            Vector2 nextPosition = BaseObject.StackedPosition;
            Vector2? lazyEndPosition = null;

            if (BaseObject is SliderTailCircle)
            {
                double trackingEndTime = Math.Max(
                    // SliderTailCircle always occurs at the final end time of the slider, but the player only needs to hold until within a lenience before it.
                    slider.EndTime + SliderEventGenerator.TAIL_LENIENCY,
                    // There's an edge case where one or more ticks/repeats fall within that leniency range.
                    // In such a case, the player needs to track until the final tick or repeat.
                    slider.NestedHitObjects.LastOrDefault(n => n is not SliderTailCircle)?.StartTime ?? double.MinValue
                );

                double lazyTravelTime = trackingEndTime - slider.StartTime;

                double endTimeMin = lazyTravelTime / slider.SpanDuration;
                if (endTimeMin % 2 >= 1)
                    endTimeMin = 1 - endTimeMin % 1;
                else
                    endTimeMin %= 1;

                lazyEndPosition = slider.StackedPosition + slider.Path.PositionAt(endTimeMin);
            }

            // Calculates end position based on if the cursor has moved enough from previous end position
            double scalingFactor = NORMALISED_RADIUS / BaseObject.Radius;

            Vector2 lastCursorPosition = lastDifficultyObject?.CursorPosition ?? LastObject.StackedPosition;

            Vector2 currMovement = nextPosition - lastCursorPosition;
            double currMovementLength = currMovement.Length * scalingFactor;

            double requiredMovementLength = BaseObject is SliderTailCircle or SliderTick ? NORMALISED_RADIUS : ASSUMED_SLIDER_RADIUS;

            if (lazyEndPosition is not null)
            {
                Vector2 lazyMovement = Vector2.Subtract((Vector2)lazyEndPosition, lastCursorPosition);

                if (lazyMovement.Length < currMovement.Length)
                    currMovement = lazyMovement;

                currMovementLength = scalingFactor * currMovement.Length;
            }

            if (currMovementLength > requiredMovementLength)
            {
                // this finds the positional delta from the required radius and the current position, and updates the currCursorPosition accordingly, as well as rewarding distance.
                Vector2 currCursorPosition = Vector2.Add(lastCursorPosition, Vector2.Multiply(currMovement, (float)((currMovementLength - requiredMovementLength) / currMovementLength)));
                CursorPosition = currCursorPosition;
            }
            else
            {
                CursorPosition = lastCursorPosition;
            }
        }

        // public double GetPrecisionBonus()
        // {
        //     return Math.Max(OverlapBonus ?? -1, SmallCircleBonus);
        // }

        // private void calculateOverlapWithNext(OsuDifficultyHitObject nextDifficultyObject)
        // {
        //     // Calculates the overlap area of the current and next circle
        //     // Then calculates a precision bonus treating that overlap area as a circle with that area
        //
        //     double r1 = IsTapObject ? BaseObject.Radius : ASSUMED_SLIDER_RADIUS / (NORMALISED_RADIUS / BaseObject.Radius);
        //     double r2 = nextDifficultyObject.IsTapObject ? nextDifficultyObject.BaseObject.Radius : ASSUMED_SLIDER_RADIUS / (NORMALISED_RADIUS / nextDifficultyObject.BaseObject.Radius);
        //
        //     double totalDistance = (nextDifficultyObject.BaseObject.StackedPosition - BaseObject.StackedPosition).Length;
        //
        //     // Return early if circles are perfectly stacked
        //     if (totalDistance <= Math.Abs(r2 - r1))
        //     {
        //         // This calculation is unnecessary, but it is useful in osu-tools
        //         OverlapCS = Math.PI * Math.Min(r1, r2) * Math.Min(r1, r2);
        //         OverlapBonus = Math.Max(0.0, 1.0 + ((30 - Math.Min(r1, r2)) / 40));
        //         return;
        //     }
        //
        //     if (totalDistance > r1 + r2)
        //     {
        //         OverlapCS = 12.2;
        //         OverlapBonus = -1;
        //         return;
        //     }
        //
        //     double d1 = (totalDistance * totalDistance + r1 * r1 - r2 * r2) / (2 * totalDistance);
        //     double d2 = (totalDistance * totalDistance + r2 * r2 - r1 * r1) / (2 * totalDistance);
        //
        //     double calcArea(double r, double d) => r * r * Math.Acos(d / r) - d * Math.Sqrt(r * r - d * d);
        //
        //     double overlapArea = calcArea(r1, d1) + calcArea(r2, d2);
        //
        //     // Calculate small circle bonus based on overlap area
        //     // Scale with angle to ensure only cases where using the overlap is realistic get the bonus
        //     double fauxRadius = Math.Sqrt((double)overlapArea / Math.PI);
        //
        //     OverlapCS = (fauxRadius - 54.4) / -4.48;
        //     OverlapBonus = Math.Max(0.0, 1.0 + (30 - fauxRadius) / 40 * Math.Pow(0.9, Math.Max(nextDifficultyObject.LazyJumpDistance, 0)));
        // }

        public DifficultyHitObject PreviousTap(int backwardsIndex)
        {
            if (!IsTapObject)
                return default;

            if (difficultyTapHitObjects is null || TapIndex is null)
                throw new NullReferenceException("Object does not contain TapObjects or TapIndex");

            int index = (int)TapIndex - (backwardsIndex + 1);
            return index >= 0 && index < difficultyTapHitObjects.Count ? difficultyTapHitObjects[index] : default;
        }

        public DifficultyHitObject NextTap(int forwardsIndex)
        {
            if (!IsTapObject)
                return default;

            if (difficultyTapHitObjects is null || TapIndex is null)
                throw new InvalidOperationException("Object does not contain TapObjects or TapIndex");

            int index = (int)TapIndex + (forwardsIndex + 1);
            return index >= 0 && index < difficultyTapHitObjects.Count ? difficultyTapHitObjects[index] : default;
        }
    }
}
