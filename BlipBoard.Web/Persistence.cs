using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlipBoard
{
    public static class Persistence
    {
        const Int32 MaxSaves = 3;

        public static void LoadLatest(BlipRepo repo, String directory)
        {
            System.Diagnostics.Debug.WriteLine("Attempting to load latest blips");

            var files = GetSaveFiles(directory);

            for (int i = 0; i < files.Length && i < MaxSaves; ++i)
            {
                Blip[] blips;

                try
                {
                    var json = File.ReadAllText(files[i]);

                    blips = JsonConvert.DeserializeObject<Blip[]>(json);

                    System.Diagnostics.Debug.WriteLine($"Loaded {blips.Length} blips");
                }
                catch (Exception)
                {
                    System.Diagnostics.Debug.WriteLine($"File {files[i]} could not be loaded, trying the previous one");

                    continue;
                }

                foreach (var blip in blips)
                {
                    repo.Add(blip);
                }

                System.Diagnostics.Debug.WriteLine($"All {repo.Blips.Count()} blips loaded");

                break;
            }

            Prune(directory);
        }

        public static void Prune(String directory)
        {
            try
            {
                var files = GetSaveFiles(directory);

                for (int i = MaxSaves; i < files.Length; ++i)
                {
                    File.Delete(files[i]);
                }
            }
            catch (Exception)
            {

            }
        }

        static String[] GetSaveFiles(String directory)
        {
            return (
                from f in Directory.GetFiles(directory, "*.json", SearchOption.TopDirectoryOnly)
                orderby File.GetCreationTime(f) descending
                select f
            ).ToArray();
        }

        public static void SaveAnother(BlipRepo repo, String directory)
        {
            Directory.CreateDirectory(directory);

            var now = DateTimeOffset.Now;

            var fileName = $"{directory}/blips-{now:yyyy-MM-dd-hh-mm-ss}.json";

            Save(repo, fileName);

            Prune(directory);
        }

        public static void Save(BlipRepo repo, String fileName)
        {
            var blips = repo.Blips.ToArray();

            var json = JsonConvert.SerializeObject(blips, Formatting.Indented);

            File.WriteAllText(fileName, json);

            System.Diagnostics.Debug.WriteLine($"Saved {blips.Length} blips");
        }

        public static void Load(BlipRepo repo, String fileName)
        {
            var json = File.ReadAllText(fileName);

            var blips = JsonConvert.DeserializeObject<Blip[]>(json);

            foreach (var blip in blips)
            {
                repo.Add(blip);
            }
        }
    }
}
