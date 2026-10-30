using LotCom.Core.Exceptions;
using LotCom.Core.Models;
using LotCom.Database.Auth;
using LotCom.Database.Caching;
using LotCom.Database.Services;
using LotComWatcher.Models.Datatypes;
using LotComWatcher.Models.Exceptions;
using LotComWatcher.Models.Factories;
using Newtonsoft.Json;

namespace LotComWatcher.Models.Services;

/// <summary>
/// Provides reading methods to pull Scanner output from the output file.
/// </summary>
public class ReaderService : IReaderService
{
    /// <summary>
    /// The raw output file that contains scan results from LotCom Scanners.
    /// </summary>
    private const string OutputFile = @"C:\LotCom\scan_out.txt";

    /// <summary>
    /// A cache mechanism for storing retrieved Processes.
    /// </summary>
    private readonly ProcessCache _processCache;

    /// <summary>
    /// A cache mechanism for storing retrieved Parts.
    /// </summary>
    private readonly PartCache _partCache;

    /// <summary>
    /// HttpClient configured to communicate with the LotCom system.
    /// </summary>
    private readonly HttpClient _httpClient;

    /// <summary>
    /// UserAgent used to authenticate API calls in the LotCom system.
    /// </summary>
    private readonly UserAgent _agent;
    
    /// <summary>
    /// Factory used to build ScanOutput objects.
    /// </summary>
    private readonly ScanOutputFactory _factory;

    /// <summary>
    /// Creates a new ReaderService.
    /// </summary>
    /// <param name="processCache"></param>
    /// <param name="partCache"></param>
    /// <param name="http"></param>
    /// <param name="agent"></param>
    public ReaderService(ProcessCache processCache, PartCache partCache, HttpClient http, UserAgent agent, ScanOutputFactory factory)
    {
        _processCache = processCache;
        _partCache = partCache;
        _httpClient = http;
        _agent = agent;
        _factory = factory;
    }

    /// <summary>
    /// Parses string-based Scan outputs to an IEnumerable of ScanOutput objects.
    /// </summary>
    /// <returns></returns>
    public async Task<IEnumerable<ScanOutput>> ParseScans(IEnumerable<string> RawScans)
    {
        // 20260918: Add the limit of threads count
        const int MaxConcurrency = 5;
        using var semaphore = new SemaphoreSlim(MaxConcurrency, MaxConcurrency);

        var tasks = RawScans.Select(async _raw =>
        {
            await semaphore.WaitAsync();
            try
            {
                return await _factory.CreateFromCSV(_raw);
            }
            catch
            {
                return (ScanOutput?)null;
            }
            finally
            {
                semaphore.Release();
            }
        }).ToList();

        ScanOutput?[] results = await Task.WhenAll(tasks);

        return results.Where(x => x != null)!;
    }

    /// <summary>
    /// Attempts to read and return all of the ScanOutputs in the Scan Output File.
    /// </summary>
    /// <returns>An IEnumerable of Scan results as ScanOutput objects.</returns>
    /// <exception cref="OutputFileAccessException"></exception>
    public async Task<IEnumerable<ScanOutput>> ReadNewScans()
    {
        // populate caches with initial Process and Part data
        IEnumerable<Process>? ProcessesFromDatabase;
        try
        {
            ProcessesFromDatabase = await ProcessService.GetAll(_httpClient, _agent);
        }
        // some database-generated issue
        catch (HttpRequestException _ex)
        {
            throw new DatabaseException("Could not retrieve Processes from the Database.", _ex);
        }
        // some formatting issue
        catch (JsonException _ex)
        {
            throw new DatabaseException("Could not process JSON response.", _ex);
        }
        // no Processes retrieved
        if (ProcessesFromDatabase is null)
        {
            ProcessesFromDatabase = [];
        }
        IEnumerable<Part>? PartsFromDatabase;
        try
        {
            PartsFromDatabase = await PartService.GetAll(_httpClient, _agent);
        }
        // some database-generated issue
        catch (HttpRequestException _ex)
        {
            throw new DatabaseException("Could not retrieve Parts from the Database.", _ex);
        }
        // some formatting issue
        catch (JsonException _ex)
        {
            throw new DatabaseException("Could not process JSON response.", _ex);
        }
        // no Parts retrieved
        if (PartsFromDatabase is null)
        {
            PartsFromDatabase = [];
        }
        _processCache.AddRange(ProcessesFromDatabase);
        _partCache.AddRange(PartsFromDatabase);
        // attempt to read the Scan Output file and throw an access exception if the read fails
        IEnumerable<string> RawScans;
        try
        {
            // save the Raw Scans from the file
            RawScans = await File.ReadAllLinesAsync(OutputFile);
        }
        catch (OperationCanceledException _ex)
        {
            throw new OutputFileAccessException
            (
                $"An exception of type {_ex.GetType()} occurred while trying to read the file at {OutputFile}:\n"
                + $"\t{_ex.Message}\n"
                + $"\t{_ex.StackTrace}"
            );
        }
        // parse ScanOutput objects from the Raw Scans
        IEnumerable<ScanOutput> ParsedScans = await ParseScans(RawScans);
        return ParsedScans;
    }

    /// <summary>
    /// Clears the contents of the configured Scanner Output file.
    /// </summary>
    /// <returns></returns>
    public async Task ClearOutputs()
    {
        // clear the file contents
        await File.WriteAllTextAsync(OutputFile, "");
        return;
    }
}