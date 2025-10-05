using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace PersonalFinanceConsoleApp
{
    /// <summary>
    /// Тип транзакции: доход или расход
    /// </summary>
    public enum TransactionType
    {
        Income,
        Expense
    }

    /// <summary>
    /// Транзакция: ID, дата, сумма, тип, описание
    /// </summary
    public class Transaction
    {
        public Guid Id { get; }
        public DateTime Date { get; }
        public decimal Amount { get; }
        public TransactionType Type { get; }
        public string Description { get; }

        public Transaction(DateTime date, decimal amount, TransactionType type, string description = null)
        {
            if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be greater than zero.");
            Id = Guid.NewGuid();
            Date = date;
            Amount = amount;
            Type = type;
            Description = description ?? string.Empty;
        }

        public override string ToString()
        {
            return $"{Date:yyyy-MM-dd} | {Type} | {Amount:F2} | {Description}";
        }
    }

    /// <summary>
    /// Группа транзакций одного типа за месяц с общей суммой и списком транзакций
    /// </summary>
    public class TransactionGroup
    {
        public TransactionType Type { get; set; }
        public decimal Total { get; set; }
        public List<Transaction> Transactions { get; set; } = new List<Transaction>();
    }

    /// <summary>
    /// Кошелёк: ID, название, валюта, начальный баланс и список транзакций
    /// </summary>
    public class Wallet
    {
        public Guid Id { get; }
        public string Name { get; }
        public string Currency { get; }
        public decimal InitialBalance { get; }
        public List<Transaction> Transactions { get; } = new List<Transaction>();

        public Wallet(string name, string currency, decimal initialBalance)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Wallet name cannot be empty.", nameof(name));
            if (string.IsNullOrWhiteSpace(currency)) throw new ArgumentException("Currency cannot be empty.", nameof(currency));
            if (initialBalance < 0) throw new ArgumentOutOfRangeException(nameof(initialBalance), "Initial balance cannot be negative.");

            Id = Guid.NewGuid();
            Name = name.Trim();
            Currency = currency.Trim();
            InitialBalance = initialBalance;
        }

        /// <summary>
        /// Текущий баланс (Initial + incomes - expenses)
        /// </summary>
        public decimal CurrentBalance
        {
            get
            {
                var income = Transactions.Where(t => t.Type == TransactionType.Income).Sum(t => t.Amount);
                var expense = Transactions.Where(t => t.Type == TransactionType.Expense).Sum(t => t.Amount);
                return InitialBalance + income - expense;
            }
        }

        /// <summary>
        /// Попытка добавить транзакцию. Если это расход и сумма больше текущего баланса — отклоняется
        /// Возвращает false и сообщение об ошибке в out-параметре
        /// </summary>
        public bool TryAddTransaction(Transaction transaction, out string error)
        {
            error = null;

            if (transaction == null)
            {
                error = "Transaction is null.";
                return false;
            }

            if (transaction.Amount <= 0)
            {
                error = "Transaction amount must be greater than zero.";
                return false;
            }

            if (transaction.Type == TransactionType.Expense && transaction.Amount > CurrentBalance)
            {
                error = $"Insufficient funds: current balance {CurrentBalance:F2} {Currency}";
                return false;
            }

            Transactions.Add(transaction);
            return true;
        }

        public bool TryAddTransaction(Transaction transaction)
        {
            return TryAddTransaction(transaction, out _);
        }

        /// <summary>
        /// Все транзакции для указанного года/месяца
        /// </summary>
        public IEnumerable<Transaction> GetTransactionsForMonth(int year, int month)
        {
            return Transactions.Where(t => t.Date.Year == year && t.Date.Month == month);
        }

        /// <summary>
        /// Группировка транзакций за месяц по типу, сортировка групп по общей сумме (по убыванию),
        /// в каждой группе транзакции сортируются по дате (от старых к новым).
        /// </summary>
        public IEnumerable<TransactionGroup> GetTransactionGroupsForMonth(int year, int month)
        {
            var groups = GetTransactionsForMonth(year, month)
                .GroupBy(t => t.Type)
                .Select(g => new TransactionGroup
                {
                    Type = g.Key,
                    Total = g.Sum(t => t.Amount),
                    Transactions = g.OrderBy(t => t.Date).ToList()
                })
                .OrderByDescending(g => g.Total)
                .ToList();

            return groups;
        }

        /// <summary>
        /// Топ-N расходов за заданный месяц, отсортированы по убыванию суммы
        /// </summary>
        public IEnumerable<Transaction> GetTopExpensesForMonth(int year, int month, int topN)
        {
            if (topN <= 0) yield break;

            foreach (var t in GetTransactionsForMonth(year, month)
                .Where(t => t.Type == TransactionType.Expense)
                .OrderByDescending(t => t.Amount)
                .Take(topN))
            {
                yield return t;
            }
        }
    }

    /// <summary>
    /// Менеджер, содержащий список кошельков, генерация данных, импорт CSV и пр
    /// </summary>
    public class FinanceManager
    {
        public List<Wallet> Wallets { get; } = new List<Wallet>();

        public Wallet CreateWallet(string name, string currency, decimal initialBalance)
        {
            var wallet = new Wallet(name, currency, initialBalance);
            Wallets.Add(wallet);
            return wallet;
        }

        public Wallet FindWalletByName(string name)
        {
            return Wallets.FirstOrDefault(w => string.Equals(w.Name, name?.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Быстрая генерация примера данных (чтобы протестировать функциональность)
        /// </summary>
        public void GenerateSampleData()
        {
            Wallets.Clear();

            var w1 = CreateWallet("Cash", "RUB", 10000m);
            w1.TryAddTransaction(new Transaction(new DateTime(2025, 9, 5), 2500m, TransactionType.Income, "Salary"));
            w1.TryAddTransaction(new Transaction(new DateTime(2025, 9, 7), 150m, TransactionType.Expense, "Groceries"));
            w1.TryAddTransaction(new Transaction(new DateTime(2025, 9, 10), 1200m, TransactionType.Expense, "Rent"));
            w1.TryAddTransaction(new Transaction(new DateTime(2025, 9, 20), 900m, TransactionType.Expense, "New Shoes"));
            w1.TryAddTransaction(new Transaction(new DateTime(2025, 8, 21), 500m, TransactionType.Income, "Gift"));

            var w2 = CreateWallet("Card", "USD", 200m);
            w2.TryAddTransaction(new Transaction(new DateTime(2025, 9, 3), 500m, TransactionType.Income, "Freelance"));
            w2.TryAddTransaction(new Transaction(new DateTime(2025, 9, 5), 60m, TransactionType.Expense, "Uber"));
            w2.TryAddTransaction(new Transaction(new DateTime(2025, 9, 15), 90m, TransactionType.Expense, "Restaurant"));
            w2.TryAddTransaction(new Transaction(new DateTime(2025, 9, 25), 300m, TransactionType.Expense, "Gadget"));

            // Пример попытки добавить слишком большой расход (проверка валидации)
            string err;
            bool added = w2.TryAddTransaction(new Transaction(new DateTime(2025, 10, 1), 500m, TransactionType.Expense, "Too big"), out err);
            if (!added)
            {
                // Для демонстрации: ничего не делаем — это ожидаемое поведение
            }
        }

        /// <summary>
        /// Загрузка из CSV. Ожидается простая структура:
        /// WalletName,Currency,InitialBalance,TransactionDate,Amount,Type,Description
        /// Пример: Card,USD,200,2025-09-03,500,Income,Freelance
        /// Поддерживаются поля в кавычках и запятые внутри кавычек.
        /// Возвращает true, если файл прочитан без фатальных ошибок; в out error возвращается агрегированное сообщение при ошибках
        /// </summary>
        public bool LoadFromCsv(string path, out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(path))
            {
                error = "Path is null or empty.";
                return false;
            }

            if (!File.Exists(path))
            {
                error = $"File not found: {path}";
                return false;
            }

            var errors = new List<string>();

            try
            {
                var lines = File.ReadAllLines(path);
                if (lines.Length <= 1)
                {
                    error = "CSV file contains no data.";
                    return false;
                }

                Wallets.Clear();

                for (int i = 1; i < lines.Length; i++)
                {
                    var line = lines[i];
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    var cols = SplitCsvLine(line);
                    if (cols.Length < 6)
                    {
                        errors.Add($"Line {i + 1}: not enough columns.");
                        continue;
                    }

                    var walletName = cols[0].Trim();
                    var currency = cols[1].Trim();

                    // Попытка распарсить initial balance — если не получилось, default = 0 (логируем)
                    if (!TryParseDecimal(cols[2], out decimal initialBalance))
                    {
                        errors.Add($"Line {i + 1}: invalid initial balance '{cols[2]}' — defaulted to 0.");
                        initialBalance = 0m;
                    }

                    // Создаём кошелёк заранее, чтобы даже при ошибочных транзакциях он существовал (для диагностики)
                    var wallet = FindWalletByName(walletName);
                    if (wallet == null)
                    {
                        try
                        {
                            wallet = CreateWallet(walletName, currency, initialBalance);
                        }
                        catch (Exception ex)
                        {
                            errors.Add($"Line {i + 1}: failed to create wallet '{walletName}': {ex.Message}");
                            // если не можем создать кошелёк — пропускаем строку
                            continue;
                        }
                    }

                    // Далее парсим дату, сумму и тип — если они неверные, логируем и пропускаем/не добавляем транзакцию
                    if (!TryParseDate(cols[3], out DateTime date))
                    {
                        errors.Add($"Line {i + 1}: invalid date '{cols[3]}' — defaulted to now.");
                        date = DateTime.Now;
                    }

                    if (!TryParseDecimal(cols[4], out decimal amount))
                    {
                        errors.Add($"Line {i + 1}: invalid amount '{cols[4]}' — skipped transaction.");
                        continue; // пропускаем строку с некорректной суммой
                    }

                    if (!TryParseTransactionType(cols[5], out TransactionType type))
                    {
                        errors.Add($"Line {i + 1}: invalid transaction type '{cols[5]}' — skipped transaction.");
                        continue;
                    }

                    var desc = cols.Length > 6 ? cols[6] : string.Empty;

                    var tx = new Transaction(date, amount, type, desc);
                    if (!wallet.TryAddTransaction(tx, out string addError))
                    {
                        errors.Add($"Line {i + 1}: transaction not added: {addError}");
                    }
                }

                if (errors.Any())
                {
                    error = string.Join(Environment.NewLine, errors);
                }

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }


        /// <summary>
        /// Простой CSV-сплиттер, понимающий поля в кавычках и экранирование двойных кавычек
        /// Возвращает массив полей (без внешних кавычек)
        /// </summary>
        private string[] SplitCsvLine(string line)
        {
            if (line == null) return new string[0];

            var result = new List<string>();
            int i = 0;
            while (i < line.Length)
            {
                if (line[i] == '"')
                {
                    // поле в кавычках
                    i++; // пропускаем начальную кавычку
                    var start = i;
                    var sb = new System.Text.StringBuilder();
                    while (i < line.Length)
                    {
                        if (line[i] == '"')
                        {
                            // может быть двойная кавычка (escaped)
                            if (i + 1 < line.Length && line[i + 1] == '"')
                            {
                                sb.Append('"');
                                i += 2;
                                continue;
                            }
                            else
                            {
                                i++; // закрывающая кавычка
                                break;
                            }
                        }
                        sb.Append(line[i]);
                        i++;
                    }

                    result.Add(sb.ToString());
                    // перейти через запятую, если есть
                    if (i < line.Length && line[i] == ',') i++;
                }
                else
                {
                    // поле без кавычек — до следующей запятой
                    var start = i;
                    while (i < line.Length && line[i] != ',') i++;
                    var field = line.Substring(start, i - start).Trim();
                    result.Add(field);
                    if (i < line.Length && line[i] == ',') i++;
                }
            }

            return result.ToArray();
        }

        private bool TryParseDecimal(string s, out decimal value)
        {
            value = 0m;
            if (string.IsNullOrWhiteSpace(s)) return false;
            s = s.Trim();

            if (decimal.TryParse(s, NumberStyles.Number, CultureInfo.CurrentCulture, out value)) return true;
            if (decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out value)) return true;
            var cleaned = s.Replace(" ", "").Replace("\u00A0", "");
            return decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
        }

        private bool TryParseDate(string s, out DateTime dt)
        {
            dt = DateTime.MinValue;
            if (string.IsNullOrWhiteSpace(s)) return false;
            s = s.Trim();

            string[] formats = { "yyyy-MM-dd", "dd.MM.yyyy", "yyyy-M-d", "M/d/yyyy", "d/M/yyyy" };
            if (DateTime.TryParseExact(s, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt)) return true;
            if (DateTime.TryParseExact(s, formats, CultureInfo.CurrentCulture, DateTimeStyles.None, out dt)) return true;
            if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt)) return true;
            if (DateTime.TryParse(s, CultureInfo.CurrentCulture, DateTimeStyles.None, out dt)) return true;

            return false;
        }

        private bool TryParseTransactionType(string s, out TransactionType type)
        {
            type = TransactionType.Expense;
            if (string.IsNullOrWhiteSpace(s)) return false;
            var t = s.Trim().ToLowerInvariant();
            if (t == "income" || t == "i") { type = TransactionType.Income; return true; }
            if (t == "expense" || t == "e") { type = TransactionType.Expense; return true; }
            return false;
        }
    }

    /// <summary>
    /// Консольное взаимодействие: меню, ввод данных, отчёт по месяцам
    /// </summary>
    public class Program
    {
        public static void Main(string[] args)
        {
            var manager = new FinanceManager();
            Console.WriteLine("Personal Finance Console App");

            while (true)
            {
                Console.WriteLine();
                Console.WriteLine("Menu:");
                Console.WriteLine("1 - Generate sample data");
                Console.WriteLine("2 - Load data from CSV file");
                Console.WriteLine("3 - Manual input (create wallet / add transactions)");
                Console.WriteLine("4 - Show report for month");
                Console.WriteLine("5 - List wallets");
                Console.WriteLine("0 - Exit");
                Console.Write("Choose option: ");

                var key = Console.ReadLine();
                if (key == "0") break;

                switch (key)
                {
                    case "1":
                        manager.GenerateSampleData();
                        Console.WriteLine("Sample data generated.");
                        break;
                    case "2":
                        Console.Write("Enter CSV path: ");
                        var path = Console.ReadLine();
                        if (manager.LoadFromCsv(path, out string err))
                        {
                            if (!string.IsNullOrEmpty(err))
                            {
                                Console.WriteLine("CSV loaded with warnings:");
                                Console.WriteLine(err);
                            }
                            else
                            {
                                Console.WriteLine("CSV loaded.");
                            }
                        }
                        else
                        {
                            Console.WriteLine("Error: " + err);
                        }

                        break;
                    case "3":
                        ManualInput(manager);
                        break;
                    case "4":
                        ShowReport(manager);
                        break;
                    case "5":
                        ListWallets(manager);
                        break;
                    default:
                        Console.WriteLine("Unknown option.");
                        break;
                }
            }
        }

        private static void ListWallets(FinanceManager manager)
        {
            if (manager == null) return;

            if (!manager.Wallets.Any())
            {
                Console.WriteLine("No wallets.");
                return;
            }

            foreach (var w in manager.Wallets)
            {
                Console.WriteLine($"{w.Name} ({w.Currency}) - Initial: {w.InitialBalance:F2} - Current: {w.CurrentBalance:F2}");
            }
        }

        private static void ManualInput(FinanceManager manager)
        {
            if (manager == null) return;

            Console.Write("Wallet name: ");
            var name = Console.ReadLine();
            Console.Write("Currency: ");
            var currency = Console.ReadLine();
            Console.Write("Initial balance: ");
            var initialStr = Console.ReadLine();
            decimal initial = 0m;
            if (!decimal.TryParse(initialStr, NumberStyles.Number, CultureInfo.CurrentCulture, out initial))
            {
                decimal.TryParse(initialStr, NumberStyles.Number, CultureInfo.InvariantCulture, out initial);
            }

            var wallet = manager.FindWalletByName(name) ?? manager.CreateWallet(name ?? "Wallet", currency ?? "UNK", initial);
            Console.WriteLine($"Wallet '{wallet.Name}' ready. Current balance: {wallet.CurrentBalance:F2} {wallet.Currency}");

            while (true)
            {
                Console.Write("Add transaction? (y/n): ");
                var ans = Console.ReadLine();
                if (ans == null || ans.Trim().ToLowerInvariant() != "y") break;

                Console.Write("Date (yyyy-MM-dd or dd.MM.yyyy): ");
                var sdate = Console.ReadLine();

                DateTime date;
                if (!DateTime.TryParseExact(sdate, new[] { "yyyy-MM-dd", "dd.MM.yyyy", "yyyy-M-d" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
                {
                    if (!DateTime.TryParse(sdate, CultureInfo.CurrentCulture, DateTimeStyles.None, out date))
                    {
                        date = DateTime.Now;
                    }
                }

                Console.Write("Amount: ");
                var sAmount = Console.ReadLine();
                decimal amount;
                if (!decimal.TryParse(sAmount, NumberStyles.Number, CultureInfo.CurrentCulture, out amount))
                {
                    decimal.TryParse(sAmount, NumberStyles.Number, CultureInfo.InvariantCulture, out amount);
                }

                Console.Write("Type (I/E): ");
                var stype = Console.ReadLine();
                var type = (stype != null && stype.Trim().ToLowerInvariant() == "i") ? TransactionType.Income : TransactionType.Expense;

                Console.Write("Description: ");
                var desc = Console.ReadLine();

                try
                {
                    var tx = new Transaction(date, amount, type, desc);
                    if (wallet.TryAddTransaction(tx, out string error))
                    {
                        Console.WriteLine("Added.");
                    }
                    else
                    {
                        Console.WriteLine("Failed: " + error);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Invalid transaction: " + ex.Message);
                }
            }
        }

        private static void ShowReport(FinanceManager manager)
        {
            if (manager == null) return;

            Console.Write("Year (e.g. 2025): ");
            var sYear = Console.ReadLine();
            Console.Write("Month (1-12): ");
            var sMonth = Console.ReadLine();

            if (!int.TryParse(sYear, out int year) || !int.TryParse(sMonth, out int month) || year <= 0 || month < 1 || month > 12)
            {
                Console.WriteLine("Invalid year/month");
                return;
            }

            foreach (var w in manager.Wallets)
            {
                Console.WriteLine();
                Console.WriteLine($"=== Wallet: {w.Name} ({w.Currency}) ===");
                Console.WriteLine($"Initial balance: {w.InitialBalance:F2}, Current balance: {w.CurrentBalance:F2}");

                var groups = w.GetTransactionGroupsForMonth(year, month);
                if (!groups.Any())
                {
                    Console.WriteLine("No transactions for this month.");
                }
                else
                {
                    Console.WriteLine($"Transactions grouped by type (sorted by total desc):");
                    foreach (var g in groups)
                    {
                        Console.WriteLine($"-- {g.Type} : Total = {g.Total:F2}");
                        foreach (var t in g.Transactions)
                        {
                            Console.WriteLine($"   {t.Date:yyyy-MM-dd} | {t.Amount:F2} | {t.Description}");
                        }
                    }
                }

                var topExpenses = w.GetTopExpensesForMonth(year, month, 3).ToList();
                Console.WriteLine();
                Console.WriteLine("Top 3 expenses for month:");
                if (!topExpenses.Any())
                {
                    Console.WriteLine("  No expenses.");
                }
                else
                {
                    int rank = 1;
                    foreach (var e in topExpenses)
                    {
                        Console.WriteLine($"  {rank}. {e.Date:yyyy-MM-dd} | {e.Amount:F2} | {e.Description}");
                        rank++;
                    }
                }
            }
        }
    }
}
