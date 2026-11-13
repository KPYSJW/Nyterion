using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.Profiling;
using UnityEditorInternal;

namespace Nytherion.Editor
{
    /// <summary>저장된 Profiler 기록을 읽어 프레임별 시간과 CPU 샘플을 내보냅니다.</summary>
    [InitializeOnLoad]
    public static class FrostStreamProfileAnalysis
    {
        private const string Output = "output/frost-stream/profile-analysis";

        static FrostStreamProfileAnalysis() => EditorApplication.update += Update;

        private static void Update()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode ||
                !File.Exists(Output + "/analyze.request")) return;
            string path = File.ReadAllText(Output + "/analyze.request").Trim();
            File.Delete(Output + "/analyze.request");
            try { Analyze(path); }
            catch (Exception error) { File.WriteAllText(Output + "/error.txt", error.ToString()); }
        }

        private sealed class Marker
        {
            public string name;
            public double totalMs;
            public double selfMs;
            public double maxMs;
            public int calls;
        }

        private sealed class Frame
        {
            public int index;
            public double frameMs;
            public long gcBytes;
            public List<Marker> markers;
        }

        private static void Analyze(string path)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("Profiler 기록 누락", path);
            Directory.CreateDirectory(Output);
            bool recording = ProfilerDriver.enabled;
            ProfilerDriver.enabled = false;
            string backup = Output + "/previous-profile.data";
            bool hasBackup = ProfilerDriver.firstFrameIndex >= 0;
            if (hasBackup) ProfilerDriver.SaveProfile(backup);
            try
            {
                if (!ProfilerDriver.LoadProfile(path, false)) throw new InvalidOperationException("Profiler 기록 불러오기 실패");
                var frames = new List<Frame>();
                var aggregate = new Dictionary<string, Marker>();
                var threads = new HashSet<string>();
                int first = ProfilerDriver.firstFrameIndex;
                int last = ProfilerDriver.lastFrameIndex;
                for (int frame = first; frame <= last; frame++)
                {
                    for (int thread = 0; thread < 256; thread++)
                    {
                        using (RawFrameDataView view = ProfilerDriver.GetRawFrameDataView(frame, thread))
                        {
                            if (!view.valid) break;
                            threads.Add(view.threadGroupName + "/" + view.threadName);
                            if (view.threadName != "Main Thread") continue;
                            var result = new Frame { index = frame, frameMs = view.frameTimeMs };
                            var markers = new Dictionary<string, Marker>();
                            for (int i = 0; i < view.sampleCount; i++)
                            {
                                string name = view.GetSampleName(i);
                                double inclusive = view.GetSampleTimeMs(i);
                                double children = 0;
                                int end = Math.Min(view.sampleCount, i + 1 + view.GetSampleChildrenCountRecursive(i));
                                for (int child = i + 1; child < end; child += 1 + view.GetSampleChildrenCountRecursive(child))
                                    children += view.GetSampleTimeMs(child);
                                double self = Math.Max(0, inclusive - children);
                                Add(markers, name, inclusive, self);
                                Add(aggregate, name, inclusive, self);
                                if (name == "GC.Alloc" && view.GetSampleMetadataCount(i) > 0)
                                    result.gcBytes += view.GetSampleMetadataAsLong(i, 0);
                            }
                            result.markers = markers.Values.OrderByDescending(marker => marker.selfMs).ToList();
                            frames.Add(result);
                        }
                    }
                }
                File.WriteAllText(Output + "/frames.json", JsonConvert.SerializeObject(frames));
                File.WriteAllText(Output + "/aggregate.json", JsonConvert.SerializeObject(aggregate.Values.OrderByDescending(item => item.selfMs), Formatting.Indented));
                File.WriteAllText(Output + "/summary.json", JsonConvert.SerializeObject(new
                {
                    first, last, count = frames.Count, threads,
                    averageMs = frames.Count > 0 ? frames.Average(frame => frame.frameMs) : 0,
                    slowest = frames.OrderByDescending(frame => frame.frameMs).Take(20).Select(frame => new
                    {
                        frame.index, frame.frameMs, frame.gcBytes, top = frame.markers.Take(25)
                    })
                }, Formatting.Indented));
            }
            finally
            {
                if (hasBackup) ProfilerDriver.LoadProfile(backup, false);
                ProfilerDriver.enabled = recording;
            }
        }

        private static void Add(Dictionary<string, Marker> markers, string name, double total, double self)
        {
            if (!markers.TryGetValue(name, out Marker marker))
            {
                marker = new Marker { name = name };
                markers.Add(name, marker);
            }
            marker.totalMs += total;
            marker.selfMs += self;
            marker.maxMs = Math.Max(marker.maxMs, total);
            marker.calls++;
        }
    }
}
