using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using WealthLedger.Application.Repositories;
using WealthLedger.Application.Services;
using WealthLedger.Contracts.Domain;
using WealthLedger.Contracts.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace WealthLedger.Infrastructure.Services;

public class OfxImportService : IOfxImportService
{
    private readonly IRepository<StatementImport> _importRepository;
    private readonly IRepository<BankTransaction> _transactionRepository;
    private readonly ILogger<OfxImportService> _logger;

    private static readonly Regex InstallmentRegex =
        new(@"[Pp]arcela\s+(\d+)\s*/\s*(\d+)", RegexOptions.Compiled);

    public OfxImportService(
        IRepository<StatementImport> importRepository,
        IRepository<BankTransaction> transactionRepository,
        ILogger<OfxImportService> logger)
    {
        _importRepository = importRepository;
        _transactionRepository = transactionRepository;
        _logger = logger;
    }

    public async Task<StatementImport> ImportOfxAsync(Stream fileContent, string fileName, StatementSource source)
    {
        var transactions = ParseOfx(fileContent);
        var now = DateTime.UtcNow;

        // Duplicate prevention: if same filename already imported, remove old data and update
        var existingImports = await _importRepository.GetAllAsync();
        var existing = existingImports.FirstOrDefault(i =>
            string.Equals(i.FileName, fileName, StringComparison.OrdinalIgnoreCase));

        if (existing != null)
        {
            // Delete old transactions belonging to this import
            var oldTransactions = (await _transactionRepository.GetAllAsync())
                .Where(t => t.StatementImportId == existing.Id)
                .ToList();
            foreach (var old in oldTransactions)
                await _transactionRepository.DeleteAsync(old.Id);

            existing.Source = source;
            existing.TransactionCount = transactions.Count;
            existing.UpdatedDate = now;
            existing = await _importRepository.UpsertAsync(existing);

            foreach (var t in transactions)
            {
                var tx = new BankTransaction
                {
                    StatementImportId = existing.Id,
                    FitId = t.FitId,
                    TransactionType = t.TransactionType,
                    Date = t.Date,
                    Amount = t.Amount,
                    Description = t.Description,
                    Category = t.Category,
                    InstallmentNumber = t.InstallmentNumber,
                    InstallmentTotal = t.InstallmentTotal,
                    CreatedDate = now,
                    UpdatedDate = now
                };
                await _transactionRepository.UpsertAsync(tx);
            }

            return existing;
        }

        // New import
        var importEntity = new StatementImport
        {
            FileName = fileName,
            Source = source,
            TransactionCount = transactions.Count,
            CreatedDate = now,
            UpdatedDate = now
        };
        importEntity = await _importRepository.UpsertAsync(importEntity);

        foreach (var t in transactions)
        {
            var tx = new BankTransaction
            {
                StatementImportId = importEntity.Id,
                FitId = t.FitId,
                TransactionType = t.TransactionType,
                Date = t.Date,
                Amount = t.Amount,
                Description = t.Description,
                Category = t.Category,
                InstallmentNumber = t.InstallmentNumber,
                InstallmentTotal = t.InstallmentTotal,
                CreatedDate = now,
                UpdatedDate = now
            };
            await _transactionRepository.UpsertAsync(tx);
        }

        return importEntity;
    }

    public async Task<IEnumerable<StatementImport>> GetImportsAsync()
    {
        return await _importRepository.GetAllAsync();
    }

    public async Task<IEnumerable<BankTransaction>> GetTransactionsByImportIdAsync(Guid importId)
    {
        var all = await _transactionRepository.GetAllAsync();
        return all.Where(t => t.StatementImportId == importId).OrderBy(t => t.Date);
    }

    public async Task<bool> DeleteImportAsync(Guid importId)
    {
        var import = await _importRepository.GetAsync(importId);
        if (import == null) return false;

        var transactions = (await _transactionRepository.GetAllAsync())
            .Where(t => t.StatementImportId == importId)
            .ToList();
        foreach (var tx in transactions)
            await _transactionRepository.DeleteAsync(tx.Id);

        await _importRepository.DeleteAsync(importId);
        return true;
    }

    // ────────────────────────────────────────────────────────────────
    //  OFX SGML + XML parser
    // ────────────────────────────────────────────────────────────────

    private record ParsedTransaction(
        string? FitId,
        string TransactionType,
        DateTime Date,
        decimal Amount,
        string Description,
        int? InstallmentNumber,
        int? InstallmentTotal,
        string? Category);

    private List<ParsedTransaction> ParseOfx(Stream stream)
    {
        try
        {
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            var raw = reader.ReadToEnd();

            var xmlContent = ExtractXmlFromOfx(raw);
            if (string.IsNullOrWhiteSpace(xmlContent))
            {
                _logger.LogWarning("OFX file contains no XML/SGML content after header stripping");
                return [];
            }

            var doc = XDocument.Parse(xmlContent);
            var stmtTrns = doc.Descendants()
                .Where(e => e.Name.LocalName == "STMTTRN")
                .ToList();

            var list = new List<ParsedTransaction>();

            foreach (var stmt in stmtTrns)
            {
                var fitId = GetValue(stmt, "FITID");
                var trnType = GetValue(stmt, "TRNTYPE") ?? "OTHER";
                var dtStr = GetValue(stmt, "DTPOSTED");
                var amtStr = GetValue(stmt, "TRNAMT");
                var memo = GetValue(stmt, "MEMO");
                var name = GetValue(stmt, "NAME");

                var desc = string.IsNullOrEmpty(memo)
                    ? (name ?? "")
                    : (string.IsNullOrEmpty(name) ? memo : $"{name} - {memo}");

                if (string.IsNullOrEmpty(dtStr) || string.IsNullOrEmpty(amtStr))
                    continue;

                if (!DateTime.TryParseExact(
                        dtStr.AsSpan(0, Math.Min(8, dtStr.Length)),
                        "yyyyMMdd", CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out var dt))
                    continue;

                if (!decimal.TryParse(
                        amtStr.Replace(",", "."),
                        NumberStyles.Any,
                        CultureInfo.InvariantCulture,
                        out var amt))
                    continue;

                // Extract installment info from description (e.g. "Parcela 2/10")
                int? installNum = null, installTotal = null;
                var installMatch = InstallmentRegex.Match(desc);
                if (installMatch.Success)
                {
                    installNum = int.Parse(installMatch.Groups[1].Value);
                    installTotal = int.Parse(installMatch.Groups[2].Value);
                }

                var category = CategorizeTransaction(desc);
                list.Add(new ParsedTransaction(
                    fitId, trnType.Trim().ToUpperInvariant(), dt, amt,
                    desc.Trim(), installNum, installTotal, category));
            }

            _logger.LogInformation("Parsed {Count} transactions from OFX", list.Count);
            return list;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse OFX file");
            return [];
        }
    }

    /// <summary>
    /// Strips the OFX SGML header and converts SGML tags to well-formed XML
    /// so that XDocument.Parse can handle it.
    /// </summary>
    private static string ExtractXmlFromOfx(string raw)
    {
        // Find the start of the OFX XML/SGML body
        var ofxIndex = raw.IndexOf("<OFX>", StringComparison.OrdinalIgnoreCase);
        if (ofxIndex < 0) return string.Empty;

        var sgml = raw[ofxIndex..];

        // OFX SGML uses tags that may not be properly closed.
        // Strategy: ensure all leaf-level tags are self-closed.
        // We process line by line and close any unclosed tags.
        sgml = CloseOpenSgmlTags(sgml);
        sgml = EscapeInvalidXmlAmpersands(sgml);

        // Wrap in XML declaration so parser is happy
        return $"<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n{sgml}";
    }

    /// <summary>
    /// Some OFX exports include raw '&' in MEMO/NAME values (e.g. "A & B"),
    /// which is invalid XML and makes XDocument.Parse fail.
    /// Escapes only ampersands that are not already valid XML entities.
    /// </summary>
    private static string EscapeInvalidXmlAmpersands(string content)
    {
        return Regex.Replace(
            content,
            @"&(?!amp;|lt;|gt;|quot;|apos;|#\d+;|#x[0-9a-fA-F]+;)",
            "&amp;");
    }

    /// <summary>
    /// In OFX SGML, leaf elements are written as &lt;TAG&gt;value without a closing tag.
    /// This method adds closing tags where missing.
    /// </summary>
    private static string CloseOpenSgmlTags(string sgml)
    {
        var sb = new StringBuilder(sgml.Length + 1024);
        var lines = sgml.Split('\n');

        // Known OFX container tags that should NOT be self-closed
        var containerTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "OFX", "SIGNONMSGSRSV1", "SONRS", "STATUS", "FI",
            "BANKMSGSRSV1", "STMTTRNRS", "STMTRS", "BANKACCTFROM", "BANKTRANLIST",
            "STMTTRN", "LEDGERBAL", "BALLIST", "BAL", "AVAILBAL",
            "CREDITCARDMSGSRSV1", "CCSTMTTRNRS", "CCSTMTRS", "CCACCTFROM",
            "INVSTMTMSGSRSV1", "INVSTMTTRNRS", "INVSTMTRS"
        };

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (string.IsNullOrEmpty(line))
            {
                sb.AppendLine();
                continue;
            }

            // Check if line is: <TAG>value  (opening tag with value, no closing tag)
            var match = Regex.Match(line, @"^<([A-Za-z0-9_.]+)>(.+)$");
            if (match.Success)
            {
                var tag = match.Groups[1].Value;
                var value = match.Groups[2].Value.Trim();

                // If value doesn't start with '<' it's a leaf value
                // But skip if it already ends with a closing tag (well-formed XML)
                if (!value.StartsWith('<') && !containerTags.Contains(tag))
                {
                    if (value.EndsWith($"</{tag}>", StringComparison.OrdinalIgnoreCase))
                    {
                        sb.AppendLine(line);
                    }
                    else
                    {
                        sb.AppendLine($"<{tag}>{value}</{tag}>");
                    }
                    continue;
                }
            }

            sb.AppendLine(line);
        }

        return sb.ToString();
    }

    // ────────────────────────────────────────────────────────────────
    //  Auto-categorization
    // ────────────────────────────────────────────────────────────────

    private static readonly (string keyword, string category)[] CategoryRules =
    [
        // Transport
        ("UBER", "Transport"),
        ("99POP", "Transport"),
        ("99 POP", "Transport"),
        ("CABIFY", "Transport"),
        ("POSTO", "Fuel"),
        ("SHELL", "Fuel"),
        ("IPIRANGA", "Fuel"),
        ("ESTACIONAMENTO", "Transport"),
        ("PARKING", "Transport"),

        // Food & Delivery
        ("IFOOD", "Food"),
        ("RAPPI", "Food"),
        ("RESTAURANTE", "Food"),
        ("REST ", "Food"),
        ("LANCHONETE", "Food"),
        ("PADARIA", "Food"),
        ("CAFE", "Food"),
        ("STARBUCKS", "Food"),
        ("MCDONALDS", "Food"),
        ("BURGER", "Food"),
        ("PIZZA", "Food"),
        ("SUSHI", "Food"),
        ("BAR ", "Food"),
        ("MERCADO", "Groceries"),
        ("SUPERMERCADO", "Groceries"),
        ("CARREFOUR", "Groceries"),
        ("PAO DE ACUCAR", "Groceries"),
        ("EXTRA HIPER", "Groceries"),
        ("ATACADAO", "Groceries"),
        ("ASSAI", "Groceries"),

        // Subscriptions
        ("NETFLIX", "Subscriptions"),
        ("SPOTIFY", "Subscriptions"),
        ("AMAZON PRIME", "Subscriptions"),
        ("DISNEY", "Subscriptions"),
        ("HBO", "Subscriptions"),
        ("APPLE.COM", "Subscriptions"),
        ("GOOGLE ", "Subscriptions"),
        ("YOUTUBE", "Subscriptions"),
        ("STEAM", "Subscriptions"),

        // Health
        ("FARMACIA", "Health"),
        ("DROGARIA", "Health"),
        ("DROGA", "Health"),
        ("HOSPITAL", "Health"),
        ("CLINICA", "Health"),
        ("MEDICO", "Health"),
        ("DENTISTA", "Health"),
        ("ACADEMIA", "Health"),
        ("SMART FIT", "Health"),

        // Shopping
        ("AMAZON", "Shopping"),
        ("MERCADOLIVRE", "Shopping"),
        ("AMERICANAS", "Shopping"),
        ("MAGAZINE", "Shopping"),
        ("SHOPEE", "Shopping"),
        ("ALIEXPRESS", "Shopping"),
        ("SHEIN", "Shopping"),

        // Utilities
        ("LIGHT", "Utilities"),
        ("ENEL", "Utilities"),
        ("COPEL", "Utilities"),
        ("SABESP", "Utilities"),
        ("CEDAE", "Utilities"),
        ("VIVO", "Telecom"),
        ("CLARO", "Telecom"),
        ("TIM", "Telecom"),
        ("OI ", "Telecom"),
        ("NET ", "Telecom"),

        // Housing
        ("ALUGUEL", "Housing"),
        ("CONDOMINIO", "Housing"),
        ("IPTU", "Housing"),

        // Education
        ("ESCOLA", "Education"),
        ("FACULDADE", "Education"),
        ("UNIVERSIDADE", "Education"),
        ("CURSO", "Education"),
        ("UDEMY", "Education"),
        ("COURSERA", "Education"),

        // Transfer/PIX
        ("PIX", "Transfer"),
        ("TRANSFER", "Transfer"),
        ("TED", "Transfer"),
        ("DOC", "Transfer"),

        // Fees
        ("TARIFA", "Fees"),
        ("IOF", "Fees"),
        ("ANUIDADE", "Fees"),
        ("JUROS", "Fees"),
    ];

    public static string? CategorizeTransaction(string description)
    {
        if (string.IsNullOrWhiteSpace(description)) return null;
        var upper = description.ToUpperInvariant();
        foreach (var (keyword, category) in CategoryRules)
        {
            if (upper.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                return category;
        }
        return "Other";
    }

    /// <summary>
    /// Re-categorizes all existing bank transactions that have no category or have "Other".
    /// </summary>
    public async Task<int> RecategorizeAllAsync()
    {
        var all = (await _transactionRepository.GetAllAsync()).ToList();
        int updated = 0;
        foreach (var tx in all)
        {
            var newCat = CategorizeTransaction(tx.Description);
            if (tx.Category != newCat)
            {
                tx.Category = newCat;
                tx.UpdatedDate = DateTime.UtcNow;
                await _transactionRepository.UpsertAsync(tx);
                updated++;
            }
        }
        return updated;
    }

    private static string? GetValue(XElement parent, string localName)
    {
        return parent.Elements()
            .FirstOrDefault(e => e.Name.LocalName == localName)?
            .Value;
    }
}
