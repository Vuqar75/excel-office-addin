using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Text;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using System.Windows.Forms;

const string Product = "Excel Data Assistant Companion";
const string Version = "0.8.0";
const int ProtocolVersion = 2;
const int Port = 47831;
DateTimeOffset startedAt = DateTimeOffset.UtcNow;
string[] allowedOrigins = ["https://vuqar75.github.io"];
string sessionToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
SemaphoreSlim commandGate = new(1, 1);
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

if (args.Length == 2 && args[0] == "--restart-after" && int.TryParse(args[1], out int parentProcessId))
{
    try { Process.GetProcessById(parentProcessId).WaitForExit(10000); } catch { }
    Thread.Sleep(300);
}

X509Certificate2 certificate = CertificateManager.GetOrCreate();
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options =>
{
    options.Listen(IPAddress.Loopback, Port, listen => listen.UseHttps(certificate));
});
builder.Services.AddCors(options => options.AddPolicy("OfficeAddIn", policy =>
    policy.WithOrigins(allowedOrigins).WithMethods("GET", "POST").WithHeaders("Content-Type", "X-EDA-Token")));

WebApplication app = builder.Build();
app.UseCors("OfficeAddIn");
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/v1"))
    {
        string origin = context.Request.Headers.Origin.ToString();
        string host = context.Request.Host.Host;
        if (!allowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase) ||
            !(IPAddress.TryParse(host, out IPAddress? address) && IPAddress.IsLoopback(address)) &&
            !string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { message = "Запрос отклонён локальным мостом." });
            return;
        }
    }
    await next();
});
bool TokenIsValid(string expected, string supplied)
{
    try
    {
        if (expected.Length != supplied.Length) return false;
        return CryptographicOperations.FixedTimeEquals(Convert.FromHexString(expected), Convert.FromHexString(supplied));
    }
    catch (FormatException) { return false; }
}
app.MapGet("/v1/health", () => Results.Json(new
{
    product = Product,
    version = Version,
    protocol = ProtocolVersion,
    sessionToken,
    startedAt,
    uptimeSeconds = (long)(DateTimeOffset.UtcNow - startedAt).TotalSeconds,
    excelRunning = Process.GetProcessesByName("EXCEL").Length > 0,
    aiConfigured = AiCredentialStore.IsConfigured,
    capabilities = new[] { "folder-import", "batch-export", "power-query", "convert-xls", "combine-csv", "split-workbook", "export-pdf", "inspect-links", "backup-workbook", "configure-ai", "ai-formula", "restart-bridge" }
}));
app.MapPost("/v1/commands", async (HttpRequest http, CommandRequest request) =>
{
    string suppliedToken = http.Headers["X-EDA-Token"].ToString();
    if (!TokenIsValid(sessionToken, suppliedToken))
        return Results.Json(new { message = "Недействительный токен локальной сессии." }, statusCode: 401);
    if (request.Source != "office-addin") return Results.BadRequest(new { message = "Неизвестный источник команды." });
    if (!System.Version.TryParse(request.Version, out System.Version? clientVersion) || clientVersion < new System.Version(3, 1, 0))
        return Results.Json(new { message = "Версия надстройки несовместима с локальным мостом.", protocol = ProtocolVersion }, statusCode: 409);
    if (!await commandGate.WaitAsync(TimeSpan.FromSeconds(1)))
        return Results.Json(new { message = "Другая локальная операция уже выполняется." }, statusCode: 423);
    try
    {
        return request.Command switch
        {
            "folder-import" => Results.Json(await DialogCommands.CombineWorkbooks()),
            "batch-export" => Results.Json(await DialogCommands.BatchExportSheets()),
            "power-query" => Results.Json(await DialogCommands.RefreshPowerQueryCopy()),
            "convert-xls" => Results.Json(await DialogCommands.ConvertLegacyWorkbooks()),
            "combine-csv" => Results.Json(await DialogCommands.CombineCsvFiles()),
            "split-workbook" => Results.Json(await DialogCommands.SplitWorkbook()),
            "export-pdf" => Results.Json(await DialogCommands.ExportSheetsToPdf()),
            "inspect-links" => Results.Json(await DialogCommands.InspectLinksAndConnections()),
            "backup-workbook" => Results.Json(await DialogCommands.BackupWorkbook()),
            "configure-ai" => Results.Json(await AiCredentialStore.Configure()),
            "ai-formula" => Results.Json(await AiFormulaService.Generate(request.Ai)),
            "restart-bridge" => Results.Json(CompanionControl.ScheduleRestart()),
            _ => Results.BadRequest(new { message = "Команда отсутствует в белом списке." })
        };
    }
    finally { commandGate.Release(); }
});

await app.RunAsync();

record CommandRequest(string Command, string Source, string Version, AiFormulaInput? Ai = null);
record AiFormulaInput(string Task, string Address, int Rows, int Columns, string[] Headers, bool HasHeaders, string CurrentFormula);
record WorkbookInspection(string[] ExternalLinks, string[] Connections);

static class AiCredentialStore
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("Excel Data Assistant AI v1");
    private static readonly string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ExcelDataAssistant");
    private static readonly string KeyPath = Path.Combine(Folder, "openai-key.dat");
    public static bool IsConfigured => File.Exists(KeyPath);

    public static Task<object> Configure()
    {
        TaskCompletionSource<object> source = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Thread thread = new(() =>
        {
            try
            {
                using Form form = new() { Text = "Настройка OpenAI для Excel Data Assistant", Width = 560, Height = 235, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false, StartPosition = FormStartPosition.CenterScreen, TopMost = true, ShowInTaskbar = true, WindowState = FormWindowState.Normal };
                Label label = new() { Left = 20, Top = 20, Width = 505, Height = 42, Text = "Введите личный API-ключ OpenAI. Он будет зашифрован для текущего пользователя Windows и не попадёт в надстройку или GitHub." };
                TextBox keyBox = new() { Left = 20, Top = 72, Width = 505, UseSystemPasswordChar = true };
                Button save = new() { Text = "Сохранить", Left = 325, Top = 120, Width = 95, DialogResult = DialogResult.OK };
                Button cancel = new() { Text = "Отмена", Left = 430, Top = 120, Width = 95, DialogResult = DialogResult.Cancel };
                form.Controls.AddRange([label, keyBox, save, cancel]); form.AcceptButton = save; form.CancelButton = cancel;
                form.Shown += (_, _) => { form.WindowState = FormWindowState.Normal; form.Show(); form.BringToFront(); form.Activate(); keyBox.Focus(); };
                if (form.ShowDialog() != DialogResult.OK) { source.SetResult(new { ok = false, message = "Настройка AI отменена." }); return; }
                string key = keyBox.Text.Trim();
                if (!key.StartsWith("sk-", StringComparison.Ordinal) || key.Length < 20) { source.SetResult(new { ok = false, message = "Ключ не похож на API-ключ OpenAI." }); return; }
                Directory.CreateDirectory(Folder);
                byte[] protectedKey = ProtectedData.Protect(Encoding.UTF8.GetBytes(key), Entropy, DataProtectionScope.CurrentUser);
                File.WriteAllBytes(KeyPath, protectedKey);
                source.SetResult(new { ok = true, message = "AI настроен. Ключ зашифрован для текущего пользователя Windows." });
            }
            catch (Exception error) { source.SetResult(new { ok = false, message = $"Не удалось сохранить AI-настройку: {error.Message}" }); }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.IsBackground = true; thread.Start();
        return source.Task;
    }

    public static string Load()
    {
        if (!IsConfigured) throw new InvalidOperationException("Сначала настройте AI через локальный мост.");
        byte[] key = ProtectedData.Unprotect(File.ReadAllBytes(KeyPath), Entropy, DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(key);
    }
}

static class AiFormulaService
{
    public static async Task<object> Generate(AiFormulaInput? input)
    {
        if (input is null) return new { ok = false, message = "Контекст формулы отсутствует." };
        if (string.IsNullOrWhiteSpace(input.Task) || input.Task.Length > 1200) return new { ok = false, message = "Опишите задачу короче 1200 символов." };
        if (input.Rows < 1 || input.Columns < 1 || (long)input.Rows * input.Columns > 100000)
            return new { ok = false, message = "Некорректный или слишком большой диапазон." };
        if ((input.Address?.Length ?? 0) > 300 || (input.CurrentFormula?.Length ?? 0) > 8192)
            return new { ok = false, message = "Контекст формулы превышает безопасный размер." };
        string key;
        try { key = AiCredentialStore.Load(); }
        catch (Exception error) { return new { ok = false, needsConfiguration = true, message = error.Message }; }
        string[] headers = (input.Headers ?? []).Take(30).Select(value => value ?? "").Select(value => value.Length > 100 ? value[..100] : value).ToArray();
        string prompt = $"Задача пользователя: {input.Task}\nДиапазон: {input.Address}; размер: {input.Rows} x {input.Columns}.\nНаличие заголовков указано пользователем явно: {(input.HasHeaders ? "первая строка содержит заголовки" : "заголовков нет")}.\nЗаголовки: {(input.HasHeaders ? string.Join(" | ", headers) : "нет")}\nТекущая формула: {input.CurrentFormula}\nЗначения ячеек не предоставлены. Верни только JSON без markdown: {{\"formula\":\"=...\",\"explanation\":\"кратко на русском языке\",\"warnings\":\"только реальный риск или пустая строка\"}}. Используй английские имена функций Excel и запятые как разделители аргументов. Не выдумывай значения, не предполагай наличие заголовков вопреки явному указанию и не изменяй книгу. Если формула уже полностью соответствует указанному диапазону и заголовкам, warnings обязательно должен быть пустой строкой. Не повторяй в warnings сведения из explanation и не предлагай корректировать уже корректные диапазоны.";
        using HttpClient client = new() { Timeout = TimeSpan.FromSeconds(60) };
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", key);
        string body = JsonSerializer.Serialize(new { model = "gpt-5-nano", instructions = "Ты эксперт по формулам Microsoft Excel. Создавай только проверяемые формулы и явно отмечай предположения.", input = prompt, reasoning = new { effort = "minimal" }, max_output_tokens = 800 });
        using HttpResponseMessage response = await client.PostAsync("https://api.openai.com/v1/responses", new StringContent(body, Encoding.UTF8, "application/json"));
        string responseBody = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            string message = $"OpenAI API: HTTP {(int)response.StatusCode}.";
            try { message = JsonDocument.Parse(responseBody).RootElement.GetProperty("error").GetProperty("message").GetString() ?? message; } catch { }
            return new { ok = false, message };
        }
        string output = ExtractOutputText(responseBody);
        (int inputTokens, int outputTokens) = ExtractUsage(responseBody);
        decimal estimatedCostUsd = inputTokens * 0.05m / 1_000_000m + outputTokens * 0.40m / 1_000_000m;
        try
        {
            int start = output.IndexOf('{'), end = output.LastIndexOf('}');
            if (start < 0 || end <= start) throw new FormatException();
            using JsonDocument result = JsonDocument.Parse(output[start..(end + 1)]);
            string formula = result.RootElement.GetProperty("formula").GetString()?.Trim() ?? "";
            string explanation = result.RootElement.TryGetProperty("explanation", out JsonElement explanationElement) ? explanationElement.GetString() ?? "" : "";
            string warnings = result.RootElement.TryGetProperty("warnings", out JsonElement warningElement) ? warningElement.GetString() ?? "" : "";
            if (!FormulaSafety.IsSafe(formula, out string safetyMessage))
                return new { ok = false, message = safetyMessage };
            return new { ok = true, formula, explanation, warnings, model = "gpt-5-nano", usage = new { inputTokens, outputTokens, totalTokens = inputTokens + outputTokens, estimatedCostUsd } };
        }
        catch { return new { ok = false, message = "Не удалось разобрать ответ AI. Повторите запрос." }; }
    }

    private static string ExtractOutputText(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("output", out JsonElement output)) return "";
        foreach (JsonElement item in output.EnumerateArray())
            if (item.TryGetProperty("content", out JsonElement content))
                foreach (JsonElement part in content.EnumerateArray())
                    if (part.TryGetProperty("type", out JsonElement type) && type.GetString() == "output_text" && part.TryGetProperty("text", out JsonElement text)) return text.GetString() ?? "";
        return "";
    }

    private static (int InputTokens, int OutputTokens) ExtractUsage(string json)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("usage", out JsonElement usage)) return (0, 0);
            int input = usage.TryGetProperty("input_tokens", out JsonElement inputElement) ? inputElement.GetInt32() : 0;
            int output = usage.TryGetProperty("output_tokens", out JsonElement outputElement) ? outputElement.GetInt32() : 0;
            return (input, output);
        }
        catch { return (0, 0); }
    }
}

static class FormulaSafety
{
    private static readonly string[] BlockedFunctions = ["WEBSERVICE", "RTD", "HYPERLINK"];

    public static bool IsSafe(string formula, out string message)
    {
        message = "";
        if (!formula.StartsWith('=') || formula.Length > 8192)
        {
            message = "AI не вернул корректную формулу.";
            return false;
        }
        if (formula.Contains('[', StringComparison.Ordinal) && formula.Contains(']', StringComparison.Ordinal))
        {
            message = "AI-формула содержит запрещённую внешнюю ссылку.";
            return false;
        }
        foreach (string function in BlockedFunctions)
            if (formula.Contains(function + "(", StringComparison.OrdinalIgnoreCase))
            {
                message = $"AI-формула содержит запрещённую функцию {function}.";
                return false;
            }
        return true;
    }
}

static class CompanionControl
{
    public static object ScheduleRestart()
    {
        string executable = Environment.ProcessPath ?? throw new InvalidOperationException("Не удалось определить путь локального моста.");
        _ = Task.Run(async () =>
        {
            await Task.Delay(800);
            ProcessStartInfo startInfo = new(executable)
            {
                UseShellExecute = false,
                WorkingDirectory = AppContext.BaseDirectory,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add("--restart-after");
            startInfo.ArgumentList.Add(Environment.ProcessId.ToString());
            Process.Start(startInfo);
            Environment.Exit(0);
        });
        return new { ok = true, message = "Локальный мост перезапускается." };
    }
}

static class DialogCommands
{
    private static readonly string[] ExcelExtensions = [".xlsx", ".xlsm", ".xlsb", ".xls"];

    public static Task<object> CombineWorkbooks() => RunSta(() =>
    {
        using FolderBrowserDialog sourceDialog = new() { Description = "Выберите папку с книгами Excel", UseDescriptionForTitle = true, ShowNewFolderButton = false };
        if (sourceDialog.ShowDialog() != DialogResult.OK) return new { ok = false, message = "Выбор папки отменён." };
        string[] files = Directory.EnumerateFiles(sourceDialog.SelectedPath, "*.*", SearchOption.TopDirectoryOnly)
            .Where(path => ExcelExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.CurrentCultureIgnoreCase).ToArray();
        if (files.Length == 0) return new { ok = false, message = "В выбранной папке нет книг Excel." };
        using SaveFileDialog saveDialog = new() { Title = "Сохранить объединённую книгу", Filter = "Книга Excel (*.xlsx)|*.xlsx", FileName = "Объединённые книги.xlsx", AddExtension = true, DefaultExt = "xlsx" };
        if (saveDialog.ShowDialog() != DialogResult.OK) return new { ok = false, message = "Сохранение отменено." };
        int sheets = ExcelAutomation.Combine(files, saveDialog.FileName);
        return new { ok = true, message = $"Объединено книг: {files.Length}; листов: {sheets}. Результат: {Path.GetFileName(saveDialog.FileName)}", count = files.Length, sheets };
    });

    public static Task<object> BatchExportSheets() => RunSta(() =>
    {
        using OpenFileDialog sourceDialog = new() { Title = "Выберите книгу Excel", Filter = "Книги Excel|*.xlsx;*.xlsm;*.xlsb;*.xls", CheckFileExists = true };
        if (sourceDialog.ShowDialog() != DialogResult.OK) return new { ok = false, message = "Выбор книги отменён." };
        using FolderBrowserDialog targetDialog = new() { Description = "Выберите папку для CSV-файлов", UseDescriptionForTitle = true, ShowNewFolderButton = true };
        if (targetDialog.ShowDialog() != DialogResult.OK) return new { ok = false, message = "Выбор папки отменён." };
        int count = ExcelAutomation.ExportSheetsToCsv(sourceDialog.FileName, targetDialog.SelectedPath);
        return new { ok = true, message = $"Экспортировано листов в CSV: {count}.", count };
    });

    public static Task<object> RefreshPowerQueryCopy() => RunSta(() =>
    {
        using OpenFileDialog sourceDialog = new() { Title = "Выберите книгу с запросами Power Query", Filter = "Книги Excel|*.xlsx;*.xlsm;*.xlsb", CheckFileExists = true };
        if (sourceDialog.ShowDialog() != DialogResult.OK) return new { ok = false, message = "Выбор книги отменён." };
        using SaveFileDialog saveDialog = new() { Title = "Сохранить обновлённую копию", Filter = "Книга Excel|*.xlsx;*.xlsm;*.xlsb", FileName = $"{Path.GetFileNameWithoutExtension(sourceDialog.FileName)}_обновлено{Path.GetExtension(sourceDialog.FileName)}", AddExtension = true };
        if (saveDialog.ShowDialog() != DialogResult.OK) return new { ok = false, message = "Сохранение отменено." };
        ExcelAutomation.RefreshCopy(sourceDialog.FileName, saveDialog.FileName);
        return new { ok = true, message = $"Запросы обновлены в копии: {Path.GetFileName(saveDialog.FileName)}" };
    });

    public static Task<object> ConvertLegacyWorkbooks() => RunSta(() =>
    {
        using OpenFileDialog sourceDialog = new() { Title = "Выберите файлы XLS", Filter = "Книги Excel 97–2003 (*.xls)|*.xls", CheckFileExists = true, Multiselect = true };
        if (sourceDialog.ShowDialog() != DialogResult.OK) return new { ok = false, message = "Выбор файлов отменён." };
        string[] files = sourceDialog.FileNames.OrderBy(path => path, StringComparer.CurrentCultureIgnoreCase).ToArray();
        using FolderBrowserDialog targetDialog = new() { Description = "Выберите папку для файлов XLSX", UseDescriptionForTitle = true, ShowNewFolderButton = true };
        if (targetDialog.ShowDialog() != DialogResult.OK) return new { ok = false, message = "Выбор папки назначения отменён." };
        int converted = ExcelAutomation.ConvertLegacyWorkbooks(files, targetDialog.SelectedPath);
        return new { ok = true, message = $"Преобразовано файлов XLS в XLSX: {converted}.", count = converted };
    });

    public static Task<object> CombineCsvFiles() => RunSta(() =>
    {
        using FolderBrowserDialog sourceDialog = new() { Description = "Выберите папку с CSV-файлами", UseDescriptionForTitle = true, ShowNewFolderButton = false };
        if (sourceDialog.ShowDialog() != DialogResult.OK) return new { ok = false, message = "Выбор папки отменён." };
        string[] files = Directory.EnumerateFiles(sourceDialog.SelectedPath, "*.csv", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.CurrentCultureIgnoreCase).ToArray();
        if (files.Length == 0) return new { ok = false, message = "В выбранной папке нет CSV-файлов." };
        using SaveFileDialog saveDialog = new() { Title = "Сохранить объединённую книгу", Filter = "Книга Excel (*.xlsx)|*.xlsx", FileName = "Объединённые CSV.xlsx", AddExtension = true, DefaultExt = "xlsx" };
        if (saveDialog.ShowDialog() != DialogResult.OK) return new { ok = false, message = "Сохранение отменено." };
        int imported = ExcelAutomation.CombineCsvFiles(files, saveDialog.FileName);
        return new { ok = true, message = $"Объединено CSV-файлов: {imported}. Результат: {Path.GetFileName(saveDialog.FileName)}", count = imported };
    });

    public static Task<object> SplitWorkbook() => RunSta(() =>
    {
        using OpenFileDialog sourceDialog = new() { Title = "Выберите книгу для разделения", Filter = "Книги Excel|*.xlsx;*.xlsm;*.xlsb;*.xls", CheckFileExists = true };
        if (sourceDialog.ShowDialog() != DialogResult.OK) return new { ok = false, message = "Выбор книги отменён." };
        using FolderBrowserDialog targetDialog = new() { Description = "Выберите папку для отдельных книг", UseDescriptionForTitle = true, ShowNewFolderButton = true };
        if (targetDialog.ShowDialog() != DialogResult.OK) return new { ok = false, message = "Выбор папки назначения отменён." };
        int count = ExcelAutomation.SplitWorkbook(sourceDialog.FileName, targetDialog.SelectedPath);
        return new { ok = true, message = $"Создано отдельных книг: {count}.", count };
    });

    public static Task<object> ExportSheetsToPdf() => RunSta(() =>
    {
        using OpenFileDialog sourceDialog = new() { Title = "Выберите книгу для экспорта PDF", Filter = "Книги Excel|*.xlsx;*.xlsm;*.xlsb;*.xls", CheckFileExists = true };
        if (sourceDialog.ShowDialog() != DialogResult.OK) return new { ok = false, message = "Выбор книги отменён." };
        using FolderBrowserDialog targetDialog = new() { Description = "Выберите папку для PDF-файлов", UseDescriptionForTitle = true, ShowNewFolderButton = true };
        if (targetDialog.ShowDialog() != DialogResult.OK) return new { ok = false, message = "Выбор папки назначения отменён." };
        int count = ExcelAutomation.ExportSheetsToPdf(sourceDialog.FileName, targetDialog.SelectedPath);
        return new { ok = true, message = $"Экспортировано листов в PDF: {count}.", count };
    });

    public static Task<object> InspectLinksAndConnections() => RunSta(() =>
    {
        using OpenFileDialog sourceDialog = new() { Title = "Выберите книгу для проверки ссылок", Filter = "Книги Excel|*.xlsx;*.xlsm;*.xlsb;*.xls", CheckFileExists = true };
        if (sourceDialog.ShowDialog() != DialogResult.OK) return new { ok = false, message = "Выбор книги отменён." };
        WorkbookInspection inspection = ExcelAutomation.InspectLinksAndConnections(sourceDialog.FileName);
        using SaveFileDialog reportDialog = new() { Title = "Сохранить отчёт о ссылках", Filter = "Текстовый отчёт (*.txt)|*.txt", FileName = $"{Path.GetFileNameWithoutExtension(sourceDialog.FileName)}_ссылки.txt", AddExtension = true, DefaultExt = "txt" };
        if (reportDialog.ShowDialog() != DialogResult.OK) return new { ok = false, message = "Сохранение отчёта отменено." };
        List<string> lines = [
            $"Книга: {Path.GetFileName(sourceDialog.FileName)}",
            $"Проверено: {DateTime.Now:yyyy-MM-dd HH:mm:ss}", "",
            $"Внешние ссылки ({inspection.ExternalLinks.Length}):",
            .. inspection.ExternalLinks.DefaultIfEmpty("Не найдены."), "",
            $"Подключения ({inspection.Connections.Length}):",
            .. inspection.Connections.DefaultIfEmpty("Не найдены.")
        ];
        File.WriteAllLines(reportDialog.FileName, lines, new UTF8Encoding(true));
        return new { ok = true, message = $"Проверка завершена. Внешних ссылок: {inspection.ExternalLinks.Length}; подключений: {inspection.Connections.Length}. Отчёт: {Path.GetFileName(reportDialog.FileName)}" };
    });

    public static Task<object> BackupWorkbook() => RunSta(() =>
    {
        using OpenFileDialog sourceDialog = new() { Title = "Выберите книгу для резервного копирования", Filter = "Книги Excel|*.xlsx;*.xlsm;*.xlsb;*.xls", CheckFileExists = true };
        if (sourceDialog.ShowDialog() != DialogResult.OK) return new { ok = false, message = "Выбор книги отменён." };
        using FolderBrowserDialog targetDialog = new() { Description = "Выберите папку для резервной копии", UseDescriptionForTitle = true, ShowNewFolderButton = true };
        if (targetDialog.ShowDialog() != DialogResult.OK) return new { ok = false, message = "Выбор папки назначения отменён." };
        string backupPath = ExcelAutomation.BackupWorkbook(sourceDialog.FileName, targetDialog.SelectedPath);
        return new { ok = true, message = $"Резервная копия создана: {Path.GetFileName(backupPath)}" };
    });

    private static Task<object> RunSta(Func<object> action)
    {
        TaskCompletionSource<object> source = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Thread thread = new(() =>
        {
            try { source.SetResult(action()); }
            catch (Exception error) { source.SetResult(new { ok = false, message = error.Message }); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        return source.Task;
    }
}

static class ExcelAutomation
{
    public static WorkbookInspection InspectLinksAndConnections(string workbookPath)
    {
        dynamic? excel = null, workbook = null;
        List<string> links = [], connections = [];
        try
        {
            excel = CreateExcel();
            workbook = excel.Workbooks.Open(workbookPath, ReadOnly: true, UpdateLinks: 0);
            try
            {
                object rawLinks = workbook.LinkSources(1);
                if (rawLinks is Array array) foreach (object? item in array) if (item is not null) links.Add(item.ToString() ?? "");
            }
            catch { }
            int count = workbook.Connections.Count;
            for (int index = 1; index <= count; index++)
            {
                dynamic connection = workbook.Connections[index];
                try { connections.Add((string)connection.Name); }
                finally { Release(connection); }
            }
            return new WorkbookInspection(links.Where(value => value.Length > 0).Distinct().ToArray(), connections.Distinct().ToArray());
        }
        finally { if (workbook is not null) { workbook.Close(false); Release(workbook); } Quit(excel); }
    }

    public static string BackupWorkbook(string sourcePath, string outputFolder)
    {
        string baseName = $"{Path.GetFileNameWithoutExtension(sourcePath)}_backup_{DateTime.Now:yyyyMMdd_HHmmss}";
        string outputPath = UniqueFilePath(outputFolder, baseName, Path.GetExtension(sourcePath));
        File.Copy(sourcePath, outputPath, false);
        return outputPath;
    }

    public static int Combine(string[] files, string outputPath)
    {
        dynamic? excel = null, target = null;
        int copied = 0;
        try
        {
            excel = CreateExcel();
            target = excel.Workbooks.Add();
            foreach (string file in files)
            {
                dynamic? source = null;
                try
                {
                    source = excel.Workbooks.Open(file, ReadOnly: true, UpdateLinks: 0);
                    int sheetCount = source.Worksheets.Count;
                    for (int index = 1; index <= sheetCount; index++)
                    {
                        dynamic sheet = source.Worksheets[index];
                        sheet.Copy(After: target.Worksheets[target.Worksheets.Count]);
                        copied++;
                        Release(sheet);
                    }
                }
                finally { if (source is not null) { source.Close(false); Release(source); } }
            }
            while (target.Worksheets.Count > copied) target.Worksheets[1].Delete();
            target.SaveAs(outputPath, FileFormat: 51);
            return copied;
        }
        finally { if (target is not null) { target.Close(false); Release(target); } Quit(excel); }
    }

    public static int ExportSheetsToCsv(string workbookPath, string outputFolder)
    {
        dynamic? excel = null, workbook = null;
        int exported = 0;
        try
        {
            excel = CreateExcel();
            workbook = excel.Workbooks.Open(workbookPath, ReadOnly: true, UpdateLinks: 0);
            int count = workbook.Worksheets.Count;
            for (int index = 1; index <= count; index++)
            {
                dynamic sheet = workbook.Worksheets[index];
                dynamic? temp = null;
                try
                {
                    sheet.Copy();
                    temp = excel.ActiveWorkbook;
                    string name = SafeFileName(RepairMojibake((string)sheet.Name));
                    string outputPath = UniqueFilePath(outputFolder, name, ".csv");
                    temp.SaveAs(outputPath, FileFormat: 62, Local: true);
                    exported++;
                }
                finally { if (temp is not null) { temp.Close(false); Release(temp); } Release(sheet); }
            }
            return exported;
        }
        finally { if (workbook is not null) { workbook.Close(false); Release(workbook); } Quit(excel); }
    }

    public static int ConvertLegacyWorkbooks(string[] files, string outputFolder)
    {
        dynamic? excel = null;
        int converted = 0;
        try
        {
            excel = CreateExcel();
            foreach (string file in files)
            {
                dynamic? workbook = null;
                try
                {
                    string outputPath = UniqueFilePath(outputFolder, Path.GetFileNameWithoutExtension(file), ".xlsx");
                    workbook = excel.Workbooks.Open(file, ReadOnly: true, UpdateLinks: 0);
                    workbook.SaveAs(outputPath, FileFormat: 51);
                    converted++;
                }
                finally { if (workbook is not null) { workbook.Close(false); Release(workbook); } }
            }
            return converted;
        }
        finally { Quit(excel); }
    }

    public static int SplitWorkbook(string workbookPath, string outputFolder)
    {
        dynamic? excel = null, workbook = null;
        int created = 0;
        try
        {
            excel = CreateExcel();
            workbook = excel.Workbooks.Open(workbookPath, ReadOnly: true, UpdateLinks: 0);
            int count = workbook.Worksheets.Count;
            for (int index = 1; index <= count; index++)
            {
                dynamic sheet = workbook.Worksheets[index];
                dynamic? target = null;
                try
                {
                    string name = SafeFileName(RepairMojibake((string)sheet.Name));
                    string outputPath = UniqueFilePath(outputFolder, name, ".xlsx");
                    sheet.Copy();
                    target = excel.ActiveWorkbook;
                    target.SaveAs(outputPath, FileFormat: 51);
                    created++;
                }
                finally { if (target is not null) { target.Close(false); Release(target); } Release(sheet); }
            }
            return created;
        }
        finally { if (workbook is not null) { workbook.Close(false); Release(workbook); } Quit(excel); }
    }

    public static int ExportSheetsToPdf(string workbookPath, string outputFolder)
    {
        dynamic? excel = null, workbook = null;
        int exported = 0;
        try
        {
            excel = CreateExcel();
            workbook = excel.Workbooks.Open(workbookPath, ReadOnly: true, UpdateLinks: 0);
            int count = workbook.Worksheets.Count;
            for (int index = 1; index <= count; index++)
            {
                dynamic sheet = workbook.Worksheets[index];
                try
                {
                    string name = SafeFileName(RepairMojibake((string)sheet.Name));
                    string outputPath = UniqueFilePath(outputFolder, name, ".pdf");
                    sheet.ExportAsFixedFormat(0, outputPath);
                    exported++;
                }
                finally { Release(sheet); }
            }
            return exported;
        }
        finally { if (workbook is not null) { workbook.Close(false); Release(workbook); } Quit(excel); }
    }

    public static int CombineCsvFiles(string[] files, string outputPath)
    {
        dynamic? excel = null, target = null;
        int imported = 0;
        try
        {
            excel = CreateExcel();
            target = excel.Workbooks.Add();
            foreach (string file in files)
            {
                dynamic? source = null, sheet = null;
                try
                {
                    source = excel.Workbooks.Open(file, ReadOnly: true, Local: true);
                    sheet = source.Worksheets[1];
                    sheet.Copy(After: target.Worksheets[target.Worksheets.Count]);
                    dynamic copied = target.Worksheets[target.Worksheets.Count];
                    copied.Name = UniqueSheetName(target, Path.GetFileNameWithoutExtension(file));
                    Release(copied);
                    imported++;
                }
                finally
                {
                    if (sheet is not null) Release(sheet);
                    if (source is not null) { source.Close(false); Release(source); }
                }
            }
            while (target.Worksheets.Count > imported) target.Worksheets[1].Delete();
            target.SaveAs(outputPath, FileFormat: 51);
            return imported;
        }
        finally { if (target is not null) { target.Close(false); Release(target); } Quit(excel); }
    }

    public static void RefreshCopy(string sourcePath, string outputPath)
    {
        File.Copy(sourcePath, outputPath, true);
        dynamic? excel = null, workbook = null;
        try
        {
            excel = CreateExcel();
            workbook = excel.Workbooks.Open(outputPath, ReadOnly: false, UpdateLinks: 0);
            workbook.RefreshAll();
            excel.CalculateUntilAsyncQueriesDone();
            workbook.Save();
        }
        finally { if (workbook is not null) { workbook.Close(false); Release(workbook); } Quit(excel); }
    }

    private static dynamic CreateExcel()
    {
        Type type = Type.GetTypeFromProgID("Excel.Application") ?? throw new InvalidOperationException("Настольный Excel не найден.");
        dynamic excel = Activator.CreateInstance(type) ?? throw new InvalidOperationException("Не удалось запустить Excel.");
        excel.Visible = false;
        excel.DisplayAlerts = false;
        excel.ScreenUpdating = false;
        return excel;
    }

    private static string SafeFileName(string value)
    {
        foreach (char invalid in Path.GetInvalidFileNameChars()) value = value.Replace(invalid, '_');
        return string.IsNullOrWhiteSpace(value) ? "Лист" : value;
    }

    private static string RepairMojibake(string value)
    {
        if (!value.Contains('Р') && !value.Contains('С')) return value;
        try
        {
            string repaired = Encoding.UTF8.GetString(Encoding.GetEncoding(1251).GetBytes(value));
            return repaired.Contains('\uFFFD') ? value : repaired;
        }
        catch { return value; }
    }

    private static string UniqueFilePath(string folder, string baseName, string extension)
    {
        string path = Path.Combine(folder, baseName + extension);
        for (int index = 2; File.Exists(path); index++) path = Path.Combine(folder, $"{baseName}_{index}{extension}");
        return path;
    }

    private static string UniqueSheetName(dynamic workbook, string value)
    {
        string baseName = SafeFileName(value).Replace(':', '_').Replace('/', '_').Replace('\\', '_').Replace('?', '_').Replace('*', '_').Replace('[', '_').Replace(']', '_');
        if (baseName.Length > 31) baseName = baseName[..31];
        string candidate = string.IsNullOrWhiteSpace(baseName) ? "CSV" : baseName;
        for (int index = 2; ; index++)
        {
            bool exists = false;
            for (int sheetIndex = 1; sheetIndex <= workbook.Worksheets.Count; sheetIndex++)
            {
                dynamic sheet = workbook.Worksheets[sheetIndex];
                try { if (string.Equals((string)sheet.Name, candidate, StringComparison.CurrentCultureIgnoreCase)) exists = true; }
                finally { Release(sheet); }
                if (exists) break;
            }
            if (!exists) return candidate;
            string suffix = $"_{index}";
            candidate = baseName[..Math.Min(baseName.Length, 31 - suffix.Length)] + suffix;
        }
    }

    private static void Quit(dynamic? excel)
    {
        if (excel is null) return;
        try { excel.Quit(); } catch { }
        Release(excel);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        GC.WaitForPendingFinalizers();
    }

    private static void Release(object? value)
    {
        if (value is not null && Marshal.IsComObject(value)) Marshal.FinalReleaseComObject(value);
    }
}

static class CertificateManager
{
    private const string Subject = "CN=Excel Data Assistant Local Companion";

    public static X509Certificate2 GetOrCreate()
    {
        using X509Store personal = new(StoreName.My, StoreLocation.CurrentUser);
        personal.Open(OpenFlags.ReadWrite);
        X509Certificate2? existing = personal.Certificates
            .Find(X509FindType.FindBySubjectDistinguishedName, Subject, false)
            .OfType<X509Certificate2>()
            .FirstOrDefault(certificate => certificate.NotAfter > DateTimeOffset.Now.AddDays(30));
        if (existing is not null) return existing;

        using RSA rsa = RSA.Create(2048);
        CertificateRequest request = new(Subject, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, false));
        SubjectAlternativeNameBuilder san = new();
        san.AddIpAddress(IPAddress.Loopback);
        san.AddDnsName("localhost");
        request.CertificateExtensions.Add(san.Build());
        X509Certificate2 created = request.CreateSelfSigned(DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now.AddYears(2));
        created = new X509Certificate2(created.Export(X509ContentType.Pfx), (string?)null, X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.UserKeySet | X509KeyStorageFlags.Exportable);
        personal.Add(created);

        using X509Store roots = new(StoreName.Root, StoreLocation.CurrentUser);
        roots.Open(OpenFlags.ReadWrite);
        roots.Add(new X509Certificate2(created.Export(X509ContentType.Cert)));
        return created;
    }
}
