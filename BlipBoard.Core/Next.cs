using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BlipBoard
{
    // load everything in the beginning

    public class StreamSettings
    {
        public TimeSpan Resolution { get; set; }

        public Int32 PageSizeLog2 { get; set; } = 10;

        public Int32 PageSize => 1 << PageSizeLog2;

        public Int32 PagesPerShift { get; set; } = 4;

        public Int32 MaxShift { get; set; } = 2;

        public Int32 MinShiftToSave { get; set; } = 0;

        public Int32 BlockCountLog2 { get; set; } = 8;

        public Int32 BlocksPerPage => PageSize >> BlockCountLog2;

        public Int32 BlockSize => 1 << BlockCountLog2;

        public Int32 LuggageSizeLog2 { get; set; } = 12;

        public Int32 LuggageSize => 1 << LuggageSizeLog2;
    }

    [DebuggerDisplay("{this.ToString()}")]
    public struct Address
    {
        public Int64 manti;
        public Int32 expo;

        public Address(Int64 time, Int32 expo)
        {
            this.manti = time;
            this.expo = expo;
        }

        public Int64 Begin => manti << expo;
        public Int64 End => (manti + 1) << expo;

        public override String ToString() => $"{manti}#{expo}";
    }

    struct FilesInfo
    {
        public String valuesPath;
        public String luggagePath;
    }

    class FileStore
    {
        private readonly String basePath;
        private readonly Int32 pageSize;
        private readonly Int32 luggageSize;

        public FileStore(String basePath, Int32 pageSize, Int32 luggageSize)
        {
            this.basePath = basePath;
            this.pageSize = pageSize;
            this.luggageSize = luggageSize;

            Directory.CreateDirectory(Path.Combine(basePath, "values"));
            Directory.CreateDirectory(Path.Combine(basePath, "luggage"));
        }

        FilesInfo GetFileInfo(Address address)
        {
            return new FilesInfo
            {
                valuesPath = Path.Combine(basePath, "values", $"values-{address}.bin"),
                luggagePath = Path.Combine(basePath, "luggage", $"luggage-{address}.bin")
            };
        }

        Address ParseAddress(String fileName)
        {
            try
            {
                fileName = Path.GetFileNameWithoutExtension(fileName);

                fileName = fileName.Split('-').Last();

                var parts = fileName.Split('#');

                if (parts.Length != 2) throw new Exception();

                return new Address
                (
                    Int32.Parse(parts[0]),
                    Int32.Parse(parts[1])
                );
            }
            catch
            {
                throw new Exception($"Invalid address {fileName}.");
            }
        }

        public Address[] List()
        {
            return Directory
                .GetFiles(Path.Combine(basePath, "values"))
                .Select(ParseAddress).ToArray();
        }

        public Page Read(Address address)
        {
            Int32 i;
            var filesInfo = GetFileInfo(address);

            var page = new Page(address, pageSize, luggageSize);
            page.address = address;

            i = 0;
            using (var stream = new FileStream(filesInfo.valuesPath, FileMode.Open))
            using (var reader = new BinaryReader(stream))
            {
                page.entries[i++].counter = reader.ReadInt64();
            }

            i = 0;
            using (var stream = new FileStream(filesInfo.luggagePath, FileMode.Open))
            using (var reader = new BinaryReader(stream))
            {
                page.entries[i++].luggage = reader.ReadBytes(luggageSize);
            }

            return page;
        }

        Byte[] TryReadAllBytes(String path)
        {
            return File.ReadAllBytes(path);
        }

        public void Write(Page page)
        {
            var filesInfo = GetFileInfo(page.address);

            using (var stream = new FileStream(filesInfo.valuesPath, FileMode.CreateNew))
            using (var writer = new BinaryWriter(stream))
            {
                for (var i = 0; i < page.entries.Length; ++i)
                {
                    writer.Write(page.entries[i].counter);
                }
            }

            using (var stream = new FileStream(filesInfo.luggagePath, FileMode.CreateNew))
            {
                for (var i = 0; i < page.entries.Length; ++i)
                {
                    var luggage = page.entries[i].luggage;

                    if (luggage == null) throw new Exception("Found entry without luggage");

                    if (luggage.Length != luggageSize) throw new Exception("Found unexpected luggage size");

                    stream.Write(luggage, 0, luggageSize);
                }
            }
        }

        public void Delete(Address address)
        {
            var filesInfo = GetFileInfo(address);

            File.Delete(filesInfo.valuesPath);

            if (File.Exists(filesInfo.luggagePath))
            {
                File.Delete(filesInfo.luggagePath);
            }
        }
    }

    class PageSink : IDisposable
    {
        FileStore fileStore;

        ConcurrentQueue<Operation> queue = new ConcurrentQueue<Operation>();

        Task workerTask;

        Boolean shutdown;

        struct Operation
        {
            public Address address;
            public Page page;
        }

        public PageSink(FileStore fileStore)
        {
            this.fileStore = fileStore;

            workerTask = Task.Factory.StartNew(Work, TaskCreationOptions.LongRunning);
        }

        public Int32 QueueLength => queue.Count;

        public void Submit(Page page)
        {
            System.Diagnostics.Debug.WriteLine($"Scheduling for saving: {page.address}");

            queue.Enqueue(new Operation { page = page, address = page.address });
        }

        public void SubmitDeletion(Address address)
        {
            System.Diagnostics.Debug.WriteLine($"Scheduling for removal: {address}");

            queue.Enqueue(new Operation { address = address });
        }

        public void Dispose()
        {
            Console.WriteLine($"Sink closing with {queue.Count} pending operations in the queue.");

            shutdown = true;

            workerTask.Wait();
        }

        void Work()
        {
            while (true)
            {
                if (queue.TryDequeue(out var operation))
                {
                    if (operation.page != null)
                    {
                        SavePage(operation.page);
                    }
                    else
                    {
                        fileStore.Delete(operation.address);
                    }
                }
                else if (shutdown)
                {
                    return;
                }
                else
                {
                    Thread.Sleep(100);
                }
            }
        }

        void SavePage(Page page)
        {
            fileStore.Write(page);
        }
    }


    class PageManager
    {
        FileStore store;
        PageSink sink;
        StreamSettings settings;

        Address current;

        ConcurrentDictionary<Address, Page> pages = new ConcurrentDictionary<Address, Page>();

        public StreamSettings Settings => settings;

        public Int32 PageCount => pages.Count;

        public PageManager(PageSink sink, FileStore store, StreamSettings settings)
        {
            this.sink = sink;
            this.store = store;
            this.settings = settings;
        }

        public void Submit(Page page)
        {
            if (page.address.expo > 0) throw new Exception("Summary pages can't be submitted from outside");

            SubmitPageWithSummaries(page);

            SetCurrent(page.address);
        }

        void SubmitPageWithSummaries(Page page)
        {
            FilePage(page);

            ScheduleForSaving(page);

            RemoveAncientPartner(page);

            CreateSummaries(page);
        }

        public void ReadStore(FileStore store, out Int64 lastTime)
        {
            var addresses = store.List();

            lastTime = 0;

            if (addresses.Length == 0) return;

            lastTime = addresses.Max(a => a.End);

            foreach (var address in addresses)
            {
                if (IsAddressAncient(lastTime, address))
                {
                    store.Delete(address);
                }
                else
                {
                    try
                    {
                        FilePage(store.Read(address));
                    }
                    catch (IOException)
                    {
                        // FIXME: log it

                        continue;
                    }

                    var end = address.End;

                    if (lastTime < end)
                    {
                        lastTime = end;

                        SetCurrent(address);
                    }
                }
            }
        }

        void SetCurrent(Address address)
        {
            if (address.expo > 0) throw new Exception("Unexpected shift for new current address");

            current = address;
        }

        public IEnumerable<Page> GetPageSequence()
        {
            var address = current;

            while (address.expo < settings.MaxShift)
            {
                if (pages.TryGetValue(address, out var page))
                {
                    yield return page;

                    ++address.manti;
                }
                else
                {
                    address.manti >>= 1;
                    ++address.expo;
                }
            }
        }

        Boolean IsAddressAncient(Int64 time, Address address)
        {
            // Those two cases shouldn't have been saved to begin with, so we're returning false.
            if (address.expo < settings.MinShiftToSave) return false;
            if (address.expo > settings.MaxShift) return false;

            return (time >> address.expo) > (address.manti >> address.expo) + settings.PagesPerShift;
        }

        Page FilePage(Page page)
        {
            return pages.AddOrUpdate(page.address, page, (a, p) => throw new Exception($"Page {a} already known."));
        }

        Page Get(Address address)
        {
            return pages.TryGetValue(address, out var page) ? page : null;
        }

        void ScheduleForSaving(Page page)
        {
            if (page.address.expo < settings.MinShiftToSave) return;

            sink.Submit(page);
        }

        void RemoveAncientPartner(Page page)
        {
            var address = new Address(page.address.manti - (Int64)settings.PagesPerShift, page.address.expo);

            if (pages.TryRemove(address, out var ancientPage) && ancientPage != null && address.expo >= settings.MinShiftToSave)
            {
                sink.SubmitDeletion(address);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"Nothing to remove at: {address}");
            }
        }

        void CreateSummaries(Page page)
        {
            var shift = page.address.expo;

            if ((page.address.manti & 1) == 1 && shift < settings.MaxShift)
            {
                // create summary page for address.time and address time & ~1

                var t0 = page.address.manti & ~1;

                var a0 = new Address(t0, shift);

                var p0 = Get(a0);
                var p1 = Get(page.address);

                if (p0 == null) return;

                var newPage = p0.Accumulate(p1);

                SubmitPageWithSummaries(newPage);
            }
        }
    }

    public class BlipStream
    {
        private readonly StreamSettings settings;
        private readonly FileStore store;
        private readonly PageSink sink;
        private readonly PageManager pageManager;
        private readonly Processor processor;
        private readonly NameStoreWorker nameStoreWorker;
        private readonly BlipTranslator translator;

        private readonly Task worker;

        private Boolean disposing;

        public Int64 now => processor.Now;

        public BlipStream(StreamSettings settings, String basePath)
        {
            this.settings = settings;

            store = new FileStore(basePath, settings.PageSize, settings.LuggageSize);

            sink = new PageSink(store);

            pageManager = new PageManager(sink, store, settings);

            processor = new Processor(pageManager, DateTimeOffset.Now.ToUnixTimeMilliseconds());

            var nameStore = new TrivialNameStore();

            nameStoreWorker = new NameStoreWorker(settings.PageSize, nameStore);

            translator = new BlipTranslator(settings, processor, pageManager, nameStoreWorker.Ingress);

            worker = Task.Factory.StartNew(Work, TaskCreationOptions.LongRunning);
        }

        public void Dispose()
        {
            disposing = true;

            worker.Wait();
        }

        public void Hit(ChannelReference reference, ref Entry entry)
        {
            translator.Hit(reference, ref entry);
        }

        void Work()
        {
            while (!disposing)
            {
                var now = DateTimeOffset.Now.ToUnixTimeMilliseconds();

                var resolution = (Int32)settings.Resolution.TotalMilliseconds;

                var gap = resolution - now % resolution;

                Thread.Sleep((Int32)gap);

                if (disposing) return;

                processor.Spool();
            }
        }
    }

    class Processor
    {
        Int64 now;

        Page page;

        PageManager pageManager;

        public Int32 PageSize => pageManager.Settings.PageSize;

        public StreamSettings Settings => pageManager.Settings;

        public Int64 Now => now;

        public Processor(PageManager pageManager, Int64 now)
        {
            this.now = now;
            this.pageManager = pageManager;
            this.page = new Page(new Address(now, 0), Settings.PageSize, Settings.LuggageSize);
        }

        public void Hit(Int32 channelNumber, ref Entry entry)
        {
            page.entries[channelNumber].Accumulate(ref entry);
        }

        public void Spool()
        {
            var address = new Address { manti = now, expo = 0 };

            Console.WriteLine($"Spooling at {address}");

            var page = new Page(new Address(now + 1, 0), Settings.PageSize, Settings.LuggageSize);

            Interlocked.Exchange(ref this.page, page);

            if (page == null) throw new Exception();

            page.address = address;

            pageManager.Submit(page);

            ++now;
        }
    }

    public struct ChannelReference
    {
        public Int32 BlockId { get; set; }
        public String Name { get; set; }
    }

    struct NumberedName
    {
        public DateTimeOffset at;
        public String name;
        public Int32 number;
    }

    interface INameStore
    {
        IEnumerable<NumberedName> Load(DateTimeOffset since);
        void Save(IEnumerable<NumberedName> items);
    }

    class TrivialNameStore : INameStore
    {
        public void Save(IEnumerable<NumberedName> items) { }

        public IEnumerable<NumberedName> Load(DateTimeOffset since)
            => Enumerable.Empty<NumberedName>();
    }

    class NameStoreWorker
    {
        private readonly Int32 pageSize;
        private readonly INameStore nameStore;

        Task task;

        ConcurrentQueue<NumberedName> queue;

        NameStoreIngress ingress;

        DateTimeOffset latest = DateTimeOffset.MinValue;

        public NameStoreIngress Ingress => ingress;

        public NameStoreWorker(Int32 pageSize, INameStore nameStore)
        {
            this.pageSize = pageSize;
            this.nameStore = nameStore;
            queue = new ConcurrentQueue<NumberedName>();

            var items = nameStore.Load(latest);

            var ingress = new NameStoreIngress(pageSize, this);

            foreach (var item in items)
            {
                HitIngress(item);
            }

            task = Task.Factory.StartNew(Work, TaskCreationOptions.LongRunning);
        }

        public void Put(String name, Int32 number)
        {
            if (task == null) return;

            queue.Enqueue(new NumberedName { name = name, number = number });
        }

        void Work()
        {
            while (true)
            {
                Save();
                Load();

                Thread.Sleep(100);
            }
        }

        void Save()
        {
            var items = new List<NumberedName>();

            while (queue.TryDequeue(out var item))
            {
                items.Add(item);
            }

            nameStore.Save(items);
        }

        void Load()
        {
            var items = nameStore.Load(latest);

            foreach (var item in items)
            {
                HitIngress(item);
            }
        }

        void HitIngress(NumberedName item)
        {
            ingress.Hit(item.name, item.number);

            if (latest < item.at) latest = item.at;
        }
    }

    class NameStoreIngress
    {
        NameStoreWorker worker;
        String[] names;

        public NameStoreIngress(Int32 pageSize, NameStoreWorker worker)
        {
            names = new String[pageSize];
            this.worker = worker;
        }

        public void Hit(String name, Int32 number)
        {
            if (names[number] == null)
            {
                names[number] = name;

                worker.Put(name, number);
            }
            else if (names[number] != name)
            {
                throw new Exception("Invalid name assertion");
            }
        }
    }

    struct TimedEntry
    {
        public Int64 begin;
        public Int64 end;
        public Entry entry;
    }


    // We need a class that knows how endpoint ids match to block ids
    // We also need to load and save this

    class BlipTranslator
    {
        private readonly Processor processor;
        private readonly StreamSettings settings;
        private readonly PageManager pageManager;
        private readonly NameStoreIngress nameStore;

        Block[] blocks;

        class Block
        {
            public ConcurrentBag<Int32> reserve = new ConcurrentBag<Int32>();

            public ConcurrentDictionary<String, Int32> mapping = new ConcurrentDictionary<String, Int32>();

            public String[] names;

            public Block(Int32 size, IEnumerable<Int32> reserve)
            {
                this.mapping = new ConcurrentDictionary<string, int>();
                this.reserve = new ConcurrentBag<int>(reserve);

                names = new string[size];
            }

            public Int32 Reserve(String name)
            {
                if (reserve.TryTake(out var result))
                {
                    names[result] = name;

                    return result;
                }
                else
                {
                    throw new Exception("Reserve bag full.");
                }
            }

            public void Free(String name)
            {
                if (mapping.TryGetValue(name, out var channelNumber))
                {
                    reserve.Add(channelNumber);
                }
            }

        }

        public BlipTranslator(StreamSettings settings, Processor processor, PageManager pageManager, NameStoreIngress nameStore)
        {
            this.settings = settings;
            this.processor = processor;
            this.pageManager = pageManager;
            this.nameStore = nameStore;
            blocks = new Block[settings.BlocksPerPage];

            for (var i = 0; i < settings.BlocksPerPage; ++i)
            {
                var reserve = Enumerable.Range(i * settings.BlockSize, settings.BlockSize);

                blocks[i] = new Block(settings.BlockSize, reserve);
            }
        }


        public void Hit(ChannelReference channel, ref Entry entry)
        {
            var channelNumber = MapHit(blocks[channel.BlockId], channel.Name);

            nameStore.Hit(channel.Name, channel.BlockId * settings.BlockSize + channelNumber);

            processor.Hit(channelNumber, ref entry);
        }



        Int32 MapHit(Block block, String name)
        {
            return block.mapping.GetOrAdd(name, block.Reserve);
        }

        public void Free(ChannelReference channel)
        {
            blocks[channel.BlockId].Free(channel.Name);
        }

        public IEnumerable<TimedEntry> GetBlips(DateTimeOffset since)
        {
            foreach (var page in pageManager.GetPageSequence())
            {
                for (var i = 0; i < settings.PageSize; ++i)
                {
                    var entry = page.entries[i];

                    if (entry.counter > 0)
                    {
                        yield return new TimedEntry()
                        {
                            entry = entry,
                            begin = page.address.Begin,
                            end = page.address.End
                        };
                    }
                }
            }
        }
    }

    public struct Entry
    {
        public Int64 counter;
        public Byte[] luggage;

        public void Accumulate(ref Entry other)
        {
            counter += other.counter;
            luggage = other.luggage;
        }

        public static Entry operator +(Entry lhs, Entry rhs)
        {
            var result = new Entry();
            result.Accumulate(ref lhs);
            result.Accumulate(ref rhs);
            return result;
        }

        public Entry(Int32 luggageSize)
        {
            counter = 0;
            luggage = new Byte[luggageSize];
        }
    }

    class Page
    {
        public Address address;
        public readonly Int32 pageSize;
        public readonly Int32 luggageSize;
        public readonly Entry[] entries;

        public Page(Address address, Int32 pageSize, Int32 luggageSize)
        {
            this.address = address;
            this.pageSize = pageSize;
            this.luggageSize = luggageSize;

            this.entries = new Entry[pageSize];
            for (var i = 0; i < pageSize; ++i)
            {
                entries[i].luggage = new Byte[luggageSize];
            }
        }

        public Page Accumulate(Page other)
        {
            if (other.address.expo != address.expo) throw new Exception("Only pages with the same exponent can be accumulated");
            if (other.address.manti != address.manti + 1) throw new Exception("Only subsequent pages can be accumulated");

            var n = pageSize;

            var result = new Page(new Address(address.manti >> 1, address.expo + 1), pageSize, luggageSize);

            var i = 0;
            for (var j = 0; j < n; ++i, j += 2) result.entries[i] = other.entries[j] + other.entries[j + 1];
            for (var j = 0; j < n; ++i, j += 2) result.entries[i] = other.entries[j] + other.entries[j + 1];

            return result;
        }
    }

    class Flooder : IDisposable
    {
        Processor processor;
        StreamSettings settings;

        CancellationTokenSource cts = new CancellationTokenSource();

        Task workerTask;

        Random rnd = new Random();

        public Flooder(Processor processor, StreamSettings settings)
        {
            this.processor = processor;
            this.settings = settings;
            workerTask = Task.Factory.StartNew(Work, TaskCreationOptions.LongRunning);
        }

        public void Dispose()
        {
            cts.Cancel();

            workerTask.Wait();
        }

        void Work()
        {
            var pageSize = settings.PageSize;

            while (!cts.Token.IsCancellationRequested)
            {
                var entry = new Entry(settings.LuggageSize);

                processor.Hit(rnd.Next(pageSize), ref entry);
            }
        }
    }
}
