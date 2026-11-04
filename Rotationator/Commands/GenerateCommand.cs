using System.Text.Json;
using OatmealDome.BinaryData;
using OatmealDome.NinLib.Byaml;
using OatmealDome.NinLib.Byaml.Dynamic;
using Rotationator.Utils;

namespace Rotationator.Commands;

public static class GenerateCommand
{
    public static void Run(string lastByamlPath, string outputByamlPath, int phaseLength, int scheduleLength, string? overridePhasesPath, uint? specifiedSeed)
    {
        uint seed = specifiedSeed ?? (uint)Environment.TickCount;
        SeadRandom random = new SeadRandom(seed);

        Console.WriteLine("Random seed: " + seed);

        dynamic lastByaml = ByamlFile.Load(lastByamlPath);

        DateTime lastBaseTime = DateTime.Parse(lastByaml["DateTime"]).ToUniversalTime();
        List<dynamic> lastPhases = lastByaml["Phases"];

        //
        // Find phase start point
        //

        DateTime referenceNow = DateTime.UtcNow;

        DateTime loopTime = lastBaseTime;

        DateTime baseTime;
        List<GambitVersusPhase> currentPhases;

        if (lastPhases.Count > 0)
        {
            int lastPhasesStartIdx = -1;

            for (int i = 0; i < lastPhases.Count; i++)
            {
                Dictionary<string, dynamic> phase = lastPhases[i];

                DateTime phaseEndTime = loopTime.AddHours((int)phase["Time"]);

                if (referenceNow >= loopTime && phaseEndTime > referenceNow)
                {
                    lastPhasesStartIdx = i;
                    break;
                }

                loopTime = phaseEndTime;
            }

            if (lastPhasesStartIdx != -1)
            {
                baseTime = loopTime;
                currentPhases = lastPhases.Skip(lastPhasesStartIdx).Select(p => new GambitVersusPhase(p)).ToList();
            }
            else if (referenceNow < lastBaseTime)
            {
                // The file starts in the future: nothing has elapsed, extend it from its own start.
                baseTime = lastBaseTime;
                currentPhases = lastPhases.Select(p => new GambitVersusPhase(p)).ToList();
                lastPhasesStartIdx = 0;
            }
            else
            {
                // Every phase in the file is over. Start a fresh schedule at the phase boundary
                // (counted from the end of the file) that contains now.
                long elapsedHours = (long)(referenceNow - loopTime).TotalHours;
                baseTime = phaseLength > 0 ? loopTime.AddHours(elapsedHours / phaseLength * phaseLength) : loopTime;
                currentPhases = new List<GambitVersusPhase>();
                lastPhasesStartIdx = lastPhases.Count; // skip the 10-year correction below
            }

            // The last phase is set to 10 years, so correct this to the correct phase length.
            // If the file ran out long ago (for example the last file Nintendo served), the phase we
            // are in is that last one: move the base forward to the phase boundary that contains now,
            // otherwise the new schedule would start in the past.
            if (lastPhasesStartIdx == lastPhases.Count - 1 && phaseLength > 0 && currentPhases.Count > 0)
            {
                long elapsedHours = (long)(referenceNow - baseTime).TotalHours;
                baseTime = baseTime.AddHours(elapsedHours / phaseLength * phaseLength);
            }

            if (currentPhases.Count > 0)
            {
                currentPhases.Last().Length = phaseLength;
            }
        }
        else
        {
            baseTime = lastBaseTime;
            currentPhases = new List<GambitVersusPhase>();
        }

        //
        // Load the override phases
        //

        Dictionary<DateTime, OverridePhase> overridePhases;

        if (overridePhasesPath != null)
        {
            string overridePhasesJson = File.ReadAllText(overridePhasesPath);
            Dictionary<string, OverridePhase> overridePhasesStrKey =
                JsonSerializer.Deserialize<Dictionary<string, OverridePhase>>(overridePhasesJson)!;

            overridePhases = overridePhasesStrKey.Select(p =>
                    new KeyValuePair<DateTime, OverridePhase>(DateTime.Parse(p.Key).ToUniversalTime(), p.Value))
                .ToDictionary();

            Console.WriteLine($"Loaded {overridePhases.Count} override phases");
        }
        else
        {
            overridePhases = new Dictionary<DateTime, OverridePhase>();
        }

        //
        // Find the maximum number of phases to add.
        //

        DateTime endTime = baseTime.AddDays(scheduleLength);

        loopTime = baseTime;

        for (int i = 0; i < currentPhases.Count; i++)
        {
            GambitVersusPhase phase = currentPhases[i];

            // This is the most convenient place to do this.
            if (overridePhases.TryGetValue(loopTime, out OverridePhase? overridePhase))
            {
                phase.ApplyOverridePhase(overridePhase);
            }

            loopTime = loopTime.AddHours(phase.Length);
        }

        DateTime newPhaseBaseTime = loopTime;

        int maximumPhases = currentPhases.Count;

        // This definitely isn't the most efficient way to do this, but it works.
        while (endTime > loopTime)
        {
            maximumPhases++;

            int length;

            if (overridePhases.TryGetValue(loopTime, out OverridePhase? phase))
            {
                length = phase.Length;
            }
            else
            {
                length = phaseLength;
            }

            loopTime = loopTime.AddHours(length);
        }

        if (maximumPhases > 256)
        {
            throw new Exception("Gambit can only load up to 256 rotations at a time");
        }

        Console.WriteLine(
            $"Generating {maximumPhases} phases to reach {endTime:O} (already have {currentPhases.Count})");

        //
        // Generate new phases to fill out the schedule
        //

        List<VersusRule> gachiRulePool = [];
        var stagePools = new Dictionary<VersusRule, List<int>>
        {
            { VersusRule.Paint, [] },
            { VersusRule.Goal, [] },
            { VersusRule.Area, [] },
            { VersusRule.Lift, [] }
        };

        DateTime currentTime = newPhaseBaseTime;

        for (int i = currentPhases.Count; i < maximumPhases; i++)
        {
            GambitVersusPhase currentPhase = new GambitVersusPhase();
            GambitVersusPhase lastPhase = i != 0 ? currentPhases[i - 1] : new GambitVersusPhase();

            if (overridePhases.TryGetValue(currentTime, out OverridePhase? overridePhase))
            {
                currentPhase.ApplyOverridePhase(overridePhase);
            }

            // Calculate next phase time

            if (currentPhase.Length <= 0)
            {
                currentPhase.Length = phaseLength;
            }

            DateTime nextPhaseTime = currentTime.AddHours(currentPhase.Length);

            // Grab the next override phase for later use

            overridePhases.TryGetValue(nextPhaseTime, out OverridePhase? nextOverridePhase);

            // Populate rules and stages

            currentPhase.RegularInfo.Rule = VersusRule.Paint;

            for (int j = currentPhase.RegularInfo.Stages.Count; j < 2; j++)
            {
                currentPhase.RegularInfo.Stages.Add(StageUtil.PickStage(currentPhase, lastPhase, nextOverridePhase,
                    VersusRule.Paint,
                    stagePools[VersusRule.Paint], random));
            }

            currentPhase.RegularInfo.Stages.Sort();

            if (currentPhase.GachiInfo.Rule == VersusRule.None)
            {
                currentPhase.GachiInfo.Rule = VersusRuleUtil.PickGachiRule(currentPhase.GachiInfo, lastPhase.GachiInfo,
                    nextOverridePhase,
                    gachiRulePool, random);
            }

            for (int j = currentPhase.GachiInfo.Stages.Count; j < 2; j++)
            {
                currentPhase.GachiInfo.Stages.Add(StageUtil.PickStage(currentPhase, lastPhase, nextOverridePhase,
                    currentPhase.GachiInfo.Rule, stagePools[currentPhase.GachiInfo.Rule], random));
            }

            currentPhase.GachiInfo.Stages.Sort();

            currentPhases.Add(currentPhase);

            currentTime = nextPhaseTime;
        }

        //
        // Write BYAML
        //

        // As a fallback in case the schedule isn't updated in time, make the last phase 10 years long.
        currentPhases.Last().Length = 24 * 365 * 10;

        // Set the new base DateTime (this is usually in the JST time zone, but it accepts UTC time as well).
        lastByaml["DateTime"] = baseTime.ToString("yyyy-MM-dd'T'HH:mm:ssK");

        // Set the new phases.
        lastByaml["Phases"] = currentPhases.Select(p => p.ToByamlPhase());

        // Add some metadata about this BYAML file and how it was built.
        lastByaml["ByamlInfo"] = new Dictionary<string, dynamic>()
        {
            { "Generator", "Rotationator 1" },
            { "GenerationTime", referenceNow.ToString("O") },
            { "BaseByamlStartTime", baseTime.ToString("O") },
            { "PhaseLength", phaseLength },
            { "ScheduleLength", scheduleLength },
            { "RandomSeed", seed.ToString() }
        };

        ByamlFile.Save(outputByamlPath, lastByaml, new ByamlSerializerSettings()
        {
            ByteOrder = ByteOrder.BigEndian,
            SupportsBinaryData = false,
            Version = ByamlVersion.One
        });

        File.WriteAllText(outputByamlPath + ".json", JsonSerializer.Serialize(lastByaml, new JsonSerializerOptions()
        {
            WriteIndented = true
        }));

        string humanReadablePath = outputByamlPath + ".txt";

        if (File.Exists(humanReadablePath))
        {
            File.Delete(humanReadablePath);
        }

        using FileStream humanReadableStream = File.OpenWrite(humanReadablePath);
        using StreamWriter humanReadableWriter = new StreamWriter(humanReadableStream);

        DateTime humanReadableTime = baseTime;

        foreach (GambitVersusPhase phase in currentPhases)
        {
            humanReadableWriter.Write(humanReadableTime.ToString("O"));
            humanReadableWriter.WriteLine($" ({phase.Length} hours)");
            humanReadableWriter.Write("Regular: ");
            humanReadableWriter.Write(NameUtils.GetRuleName(phase.RegularInfo.Rule));
            humanReadableWriter.Write(" / ");
            humanReadableWriter.WriteLine(string.Join(", ", phase.RegularInfo.Stages.Select(NameUtils.GetStageName)));
            humanReadableWriter.Write("Gachi: ");
            humanReadableWriter.Write(NameUtils.GetRuleName(phase.GachiInfo.Rule));
            humanReadableWriter.Write(" / ");
            humanReadableWriter.WriteLine(string.Join(", ", phase.GachiInfo.Stages.Select(NameUtils.GetStageName)));
            humanReadableWriter.WriteLine();

            humanReadableTime = humanReadableTime.AddHours(phase.Length);
        }

        Console.WriteLine("Done!");
    }
}