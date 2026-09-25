using System.Collections.Generic;
using UnityEngine;

namespace Gluttony
{
    public class LevelGenerator : MonoBehaviour
    {
        private const float SpawnAhead = 12f;
        private const float DespawnBelow = 4f;

        [SerializeField] private Platform platformPrefab;
        [SerializeField] private Platform movingPlatformPrefab;
        [SerializeField] private Platform launchPadPrefab;
        [SerializeField] private Platform landingPrefab;
        [SerializeField] private CarnivorousPlant plantPrefab;
        [SerializeField] private SeaMine minePrefab;

        [SerializeField] private int climbRows = 6;
        [SerializeField] private int climbRowsPerSection = 1;
        [SerializeField] private int climbRowsMax = 12;
        [SerializeField] private int patternBars = 2;
        [SerializeField] private int patternBarsMax = 4;

        private static readonly int[] WidePattern = { 0, 1 };
        private static readonly int[] SweepPattern = { 0, 1, 2, 1 };

        private sealed class RowPlan
        {
            public int Jump;
            public int[] Moving;
            public bool Wide;
        }

        private static readonly string[] Intros = { "..X.X.X.", "..X.X.XX", "....X.X.", "..X...X." };

        private static readonly string[][] Tiers =
        {
            new[] { "X.X.X.X.", "X.X.X...", "X...X.X.", "X.X...X.", "X...X..." },
            new[] { "X.X.X.XX", "X.X.XXX.", "XX..X.X.", "X..X..X.", "X.XX.X..", "XX.X.X.." },
            new[] { "XX.XX.X.", "X.XXX.X.", "XXXX.X..", "X.X.XXXX", "XX.X.XX.", "X..XX.XX" },
        };

        public Platform StartPlatform { get; private set; }

        private System.Random rng;
        private CatController cat;
        private float nextY;
        private int rowsLeft;
        private int section;
        private int rowIndex;
        private int landLane;
        private int planLand;
        private int planPrev;
        private RowPlan planPrevious;
        private readonly List<RowPlan> plans = new List<RowPlan>();
        private string lastPattern;
        private Platform waitingPad;
        private readonly List<float> landings = new List<float>();

        public int SectionAt(float y)
        {
            int count = 0;
            foreach (float landing in landings)
                if (y >= landing - 0.01f)
                    count++;
            return count;
        }

        public void BeginRun(int seed, float cameraTop)
        {
            Clear();
            cat = CatController.Instance;
            rng = new System.Random(seed);
            landings.Clear();
            section = 0;
            lastPattern = null;
            waitingPad = null;
            StartPlatform = SpawnPlatform(platformPrefab, PlatformKind.Static, 0f, 0f, Lanes.PlayHalfWidth * 2f);
            StartClimb(0f);
            Fill(cameraTop);
        }

        public void Fill(float cameraTop)
        {
            while (waitingPad == null && nextY < cameraTop + SpawnAhead)
                SpawnNext();
        }

        public void Cull(float cameraBottom)
        {
            float limit = cameraBottom - DespawnBelow;
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (child.position.y < limit)
                    Remove(child.gameObject);
            }
        }

        public void ClearEnemies()
        {
            for (int i = Enemy.All.Count - 1; i >= 0; i--)
            {
                if (i >= Enemy.All.Count)
                    continue;
                var enemy = Enemy.All[i];
                if (enemy.transform.parent != transform)
                    continue;
                Fx.Dust(enemy.HitRect.center, 3, 0.6f);
                enemy.Consume();
            }
        }

        private void Clear()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
                Remove(transform.GetChild(i).gameObject);
        }

        private static void Remove(GameObject go)
        {
            go.SetActive(false);
            Destroy(go);
        }

        private void StartClimb(float baseY)
        {
            nextY = baseY + CatController.RowHeight;
            rowsLeft = Mathf.Min(climbRowsMax, climbRows + climbRowsPerSection * section);
            rowIndex = 0;
            landLane = -1;
            planLand = -1;
            planPrev = -1;
            planPrevious = null;
            plans.Clear();
        }

        private void SpawnNext()
        {
            if (rowsLeft <= 0)
            {
                SpawnLaunch(nextY);
                return;
            }
            SpawnRow();
            nextY += CatController.RowHeight;
            rowsLeft--;
            rowIndex++;
        }

        private void SpawnRow()
        {
            while (plans.Count < 2)
            {
                var planned = PlanRow(planLand, planPrev, planPrevious, rowIndex + plans.Count);
                plans.Add(planned);
                planPrevious = planned;
                planPrev = planned.Moving != null ? -1 : planLand;
                planLand = planned.Jump;
            }
            var plan = plans[0];
            plans.RemoveAt(0);

            if (plan.Moving == null)
            {
                int from = landLane < 0 ? plan.Jump : Mathf.Min(landLane, plan.Jump);
                int to = landLane < 0 ? plan.Jump : Mathf.Max(landLane, plan.Jump);
                AddDecoy(ref from, ref to, plan.Jump, plans[0]);
                SpawnPlatform(platformPrefab, PlatformKind.Static, SpanCenter(from, to), nextY, SpanWidth(from, to));
            }
            else if (plan.Wide)
            {
                var wide = SpawnPlatform(movingPlatformPrefab, PlatformKind.Moving, 0f, nextY, Lanes.Width * 2f);
                wide.SetMoving(new[] { SpanCenter(0, 1), SpanCenter(1, 2) }, WidePattern, 1);
            }
            else
            {
                var single = SpawnPlatform(movingPlatformPrefab, PlatformKind.Moving, 0f, nextY, Lanes.Width);
                single.SetMoving(new[] { Lanes.X(0), Lanes.X(1), Lanes.X(2) }, plan.Moving, 1);
            }
            landLane = plan.Jump;
        }

        private RowPlan PlanRow(int land, int prev, RowPlan previous, int index)
        {
            if (land < 0)
                return new RowPlan { Jump = rng.Next(Lanes.Count) };
            bool inClimb = index < rowIndex + rowsLeft;
            bool afterMoving = previous != null && previous.Moving != null;
            if (afterMoving && !previous.Wide && previous.Moving.Length == 2)
            {
                if (inClimb && section >= 1 && Chance(Mathf.Min(0.6f, 0.15f * section)))
                    return ChainRow(previous);
                return new RowPlan { Jump = ThirdLane(previous.Moving) };
            }
            bool canMove = !afterMoving && inClimb;
            int dir = prev < 0 ? 0 : System.Math.Sign(land - prev);
            if (dir != 0)
            {
                int ahead = land + dir;
                if (ahead >= 0 && ahead < Lanes.Count && Chance(0.6f))
                    return new RowPlan { Jump = ahead };
                if (canMove && Chance(Mathf.Min(0.9f, 0.5f + 0.1f * section)))
                    return MovingRow(land, prev);
                int back = prev - dir;
                if (Mathf.Abs(land - prev) == 1 && back >= 0 && back < Lanes.Count && Chance(0.5f))
                    return new RowPlan { Jump = back };
                return new RowPlan { Jump = land };
            }

            if (canMove && Chance(Mathf.Min(0.5f, 0.1f + 0.07f * section)))
                return MovingRow(land, prev);
            var moves = new List<int>();
            for (int l = 0; l < Lanes.Count; l++)
            {
                if (l == land)
                    continue;
                moves.Add(l);
                if (Mathf.Abs(l - land) == 2 && Chance(Mathf.Min(0.5f, 0.15f + 0.1f * section)))
                    moves.Add(l);
            }
            return new RowPlan { Jump = moves[rng.Next(moves.Count)] };
        }

        private RowPlan MovingRow(int land, int prev)
        {
            if (Chance(Mathf.Max(0.05f, 0.25f - 0.05f * section)))
                return new RowPlan { Jump = rng.Next(Lanes.Count), Moving = WidePattern, Wide = true };
            if (section >= 2 && Chance(0.35f))
                return new RowPlan { Jump = rng.Next(Lanes.Count), Moving = SweepPattern };
            var others = new List<int>();
            for (int l = 0; l < Lanes.Count; l++)
                if (l != land && l != prev)
                    others.Add(l);
            int other = others[rng.Next(others.Count)];
            var pattern = Chance(0.5f) ? new[] { land, other } : new[] { other, land };
            return new RowPlan { Jump = Chance(0.7f) ? other : land, Moving = pattern };
        }

        private RowPlan ChainRow(RowPlan previous)
        {
            int jump = previous.Jump;
            int at = previous.Moving[0] == jump ? 0 : 1;
            var pattern = new int[2];
            pattern[at] = jump;
            pattern[1 - at] = ThirdLane(previous.Moving);
            return new RowPlan { Jump = Chance(0.7f) ? pattern[1 - at] : jump, Moving = pattern };
        }

        private static int ThirdLane(int[] pair) => Lanes.Count * (Lanes.Count - 1) / 2 - pair[0] - pair[1];

        private void AddDecoy(ref int from, ref int to, int nextLand, RowPlan next)
        {
            if (!Chance(Mathf.Max(0f, 0.5f - 0.1f * section)))
                return;
            int nextFrom;
            int nextTo;
            if (next.Moving == null)
            {
                nextFrom = Mathf.Min(nextLand, next.Jump);
                nextTo = Mathf.Max(nextLand, next.Jump);
            }
            else if (next.Wide)
            {
                nextFrom = 0;
                nextTo = Lanes.Count - 1;
            }
            else
            {
                nextFrom = Mathf.Min(next.Moving);
                nextTo = Mathf.Max(next.Moving);
            }
            var sides = new List<int>();
            if (from > 0 && (from - 1 < nextFrom || from - 1 > nextTo))
                sides.Add(from - 1);
            if (to < Lanes.Count - 1 && (to + 1 < nextFrom || to + 1 > nextTo))
                sides.Add(to + 1);
            if (sides.Count == 0)
                return;
            int lane = sides[rng.Next(sides.Count)];
            from = Mathf.Min(from, lane);
            to = Mathf.Max(to, lane);
        }

        private static float SpanCenter(int from, int to) => (Lanes.X(from) + Lanes.X(to)) * 0.5f;
        private static float SpanWidth(int from, int to) => (to - from + 1) * Lanes.Width;

        private void SpawnLaunch(float padY)
        {
            int sector = landLane < 0 ? 1 : landLane;
            var pad = SpawnPlatform(launchPadPrefab, PlatformKind.Launch, Lanes.X(sector), padY, Lanes.Width);

            int bars = Mathf.Min(patternBarsMax, patternBars + section / 2);
            pad.Launch.Bars = bars;
            pad.Launch.CruiseBeats = 4 * (1 + bars);
            waitingPad = pad;
        }

        public void OpenCorridor(Platform pad, float startX)
        {
            if (pad == null || pad != waitingPad)
                return;
            waitingPad = null;
            var launch = pad.Launch;
            float tableY = launch.StartY + cat.LaunchRise(launch.CruiseBeats) - CatController.TableDrop;
            FillCorridor(launch, launch.StartY, startX, launch.Bars, tableY - 1f);

            SpawnPlatform(landingPrefab, PlatformKind.Landing, 0f, tableY, Lanes.PlayHalfWidth * 2f);
            landings.Add(tableY);
            section++;
            StartClimb(tableY);
        }

        private void FillCorridor(LaunchInfo launch, float startY, float startX, int bars, float maxY)
        {
            var hits = new List<double>();
            var rests = new List<double>();
            AddBar(hits, rests, section == 0 ? Intros[0] : Intros[rng.Next(Intros.Length)], 0);
            for (int bar = 1; bar <= bars; bar++)
                AddBar(hits, rests, PickPattern(), bar * 4);

            float tip = cat.Skewer.TipHeight;
            float speed = cat.LaunchUnitsPerBeat;
            hits.RemoveAll(beat => startY + tip + speed * (float)beat > maxY);
            rests.RemoveAll(beat => startY + tip + speed * (float)beat > maxY);
            int lane = Lanes.Nearest(startX);
            int shape = 0;
            int direction = 1;
            int previousBar = -1;
            double previous = double.NaN;
            var pathBeats = new List<double>();
            var pathLanes = new List<int>();
            foreach (double beat in hits)
            {
                int bar = (int)(beat / 4.0);
                if (!double.IsNaN(previous))
                {
                    bool barStart = bar != previousBar;
                    if (barStart)
                    {
                        shape = PickShape();
                        direction = Chance(0.5f) ? 1 : -1;
                    }
                    double gap = beat - previous;
                    bool canMove = gap >= 0.99 || (gap >= 0.49 && section >= 2 && Chance(0.3f));
                    if (canMove)
                        lane = NextLane(lane, shape, ref direction, gap >= 1.49, barStart);
                }
                previous = beat;
                previousBar = bar;

                var plant = Instantiate(plantPrefab, transform);
                plant.Launch = launch;
                plant.ImpactBeats = beat;
                plant.PlaceForImpact(Lanes.X(lane), startY + tip + speed * (float)beat);
                pathBeats.Add(beat);
                pathLanes.Add(lane);
            }

            float restChance = Mathf.Min(0.8f, 0.12f + 0.12f * section);
            float hitChance = section < 2 ? 0f : Mathf.Min(0.5f, 0.1f * section);
            float mineY = startY + tip;
            foreach (double beat in rests)
                if (Chance(restChance))
                    PlaceMine(launch, beat, true, pathBeats, pathLanes, mineY, speed);
            foreach (double beat in pathBeats)
                if (Chance(hitChance))
                    PlaceMine(launch, beat, false, pathBeats, pathLanes, mineY, speed);
        }

        private void PlaceMine(LaunchInfo launch, double beat, bool rest, List<double> beats, List<int> lanes, float baseY, float speed)
        {
            int after = beats.Count;
            for (int i = 0; i < beats.Count; i++)
            {
                if (beats[i] > beat + 0.001)
                {
                    after = i;
                    break;
                }
            }
            int before = after - 1;
            if (before < 0 || after >= beats.Count)
                return;
            int from = lanes[before];
            int to = lanes[after];
            var blocked = new bool[Lanes.Count];
            for (int l = Mathf.Min(from, to); l <= Mathf.Max(from, to); l++)
                blocked[l] = true;
            bool soonInFrom = false;
            for (int i = after; i < beats.Count && beats[i] <= beat + 1.001; i++)
            {
                blocked[lanes[i]] = true;
                soonInFrom |= lanes[i] == from;
            }
            var crossed = new bool[Lanes.Count];
            for (int i = before; i + 1 < beats.Count && beats[i] <= beat + 0.6; i++)
            {
                if (beats[i] < beat - 0.1)
                    continue;
                for (int l = Mathf.Min(lanes[i], lanes[i + 1]); l <= Mathf.Max(lanes[i], lanes[i + 1]); l++)
                    crossed[l] = true;
            }
            var options = new List<int>();
            for (int l = 0; l < Lanes.Count; l++)
                if (!blocked[l] && !crossed[l])
                    options.Add(l);
            bool squeeze = rest && section >= 1 && from != to && !soonInFrom && !crossed[from] && beat - beats[before] >= 0.49 && beats[after] - beat >= 0.49;
            if (squeeze)
                options.Add(from);
            if (options.Count == 0)
                return;
            var mine = Instantiate(minePrefab, transform);
            mine.Launch = launch;
            mine.ImpactBeats = beat;
            mine.PlaceForImpact(Lanes.X(options[rng.Next(options.Count)]), baseY + speed * (float)beat);
        }

        private int PickShape()
        {
            float roll = (float)rng.NextDouble();
            float stay = section == 0 ? 0.4f : 0.2f;
            if (roll < stay)
                return 0;
            if (roll < stay + 0.3f)
                return 1;
            if (roll < stay + 0.55f)
                return 2;
            return 3;
        }

        private int NextLane(int lane, int shape, ref int direction, bool wide, bool barStart)
        {
            switch (shape)
            {
                case 0:
                    return barStart && Chance(0.5f) ? OtherLane(lane, wide) : lane;
                case 1:
                case 2:
                    int next = lane + direction;
                    if (next < 0 || next >= Lanes.Count)
                    {
                        direction = -direction;
                        next = lane + direction;
                    }
                    if (shape == 2)
                        direction = -direction;
                    return next;
                default:
                    return OtherLane(lane, wide);
            }
        }

        private int OtherLane(int lane, bool wide)
        {
            int reach = wide ? 2 : 1;
            var candidates = new List<int>();
            for (int l = 0; l < Lanes.Count; l++)
                if (l != lane && Mathf.Abs(l - lane) <= reach)
                    candidates.Add(l);
            return candidates[rng.Next(candidates.Count)];
        }

        private static void AddBar(List<double> hits, List<double> rests, string pattern, int offset)
        {
            for (int step = 0; step < pattern.Length; step++)
            {
                double beat = offset + step * 0.5;
                if (pattern[step] == 'X')
                    hits.Add(beat);
                else if (beat >= 1.5)
                    rests.Add(beat);
            }
        }

        private string PickPattern()
        {
            int tier;
            if (section == 0)
                tier = 0;
            else if (section == 1)
                tier = Chance(0.6f) ? 1 : 0;
            else if (section <= 3)
                tier = Chance(0.5f) ? 2 : 1;
            else
                tier = Chance(0.7f) ? 2 : 1;

            var options = Tiers[tier];
            string pattern = options[rng.Next(options.Length)];
            if (pattern == lastPattern)
                pattern = options[(System.Array.IndexOf(options, pattern) + 1 + rng.Next(options.Length - 1)) % options.Length];
            lastPattern = pattern;
            return pattern;
        }

        private Platform SpawnPlatform(Platform prefab, PlatformKind kind, float x, float y, float width)
        {
            var platform = Instantiate(prefab, new Vector3(x, y, 0f), Quaternion.identity, transform);
            platform.Setup(kind, width);
            return platform;
        }

        private bool Chance(float p) => rng.NextDouble() < p;
    }
}
