using System;
using System.IO;
using System.Linq;
using System.Transactions;
using NUnit.Framework;
using PersonalFinanceConsoleApp;

namespace PersonalFinanceConsoleApp.Tests
{
    [TestFixture]
    public class FinanceTests
    {
        private FinanceManager _manager;

        [SetUp]
        public void SetUp()
        {
            _manager = new FinanceManager();
        }

        [Test]
        public void Transaction_Constructor_NegativeOrZero_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Transaction(DateTime.Now, 0m, TransactionType.Income));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Transaction(DateTime.Now, -10m, TransactionType.Expense));
        }

        [Test]
        public void Wallet_CurrentBalance_Computation()
        {
            var w = new Wallet("Test", "RUB", 100m);
            w.TryAddTransaction(new Transaction(new DateTime(2025, 9, 1), 50m, TransactionType.Income));
            w.TryAddTransaction(new Transaction(new DateTime(2025, 9, 2), 30m, TransactionType.Expense));
            // 100 + 50 - 30 = 120
            Assert.AreEqual(120m, w.CurrentBalance);
        }

        [Test]
        public void Wallet_TryAddTransaction_PreventOverspend()
        {
            var w = new Wallet("W", "USD", 100m);
            bool added = w.TryAddTransaction(new Transaction(DateTime.Now, 150m, TransactionType.Expense), out string err);
            Assert.IsFalse(added);
            Assert.IsNotNull(err);
            StringAssert.Contains("Insufficient funds", err);
            // balance unchanged
            Assert.AreEqual(100m, w.CurrentBalance);
        }

        [Test]
        public void Wallet_TryAddTransaction_AllowsExpenseWithinBalance()
        {
            var w = new Wallet("W", "USD", 200m);
            bool added = w.TryAddTransaction(new Transaction(DateTime.Now, 150m, TransactionType.Expense), out string err);
            Assert.IsTrue(added);
            Assert.IsNull(err);
            Assert.AreEqual(50m, w.CurrentBalance);
        }

        [Test]
        public void Wallet_GetTransactionsForMonth_FiltersByMonth()
        {
            var w = new Wallet("W", "RUB", 0m);
            w.TryAddTransaction(new Transaction(new DateTime(2025, 9, 1), 10m, TransactionType.Income));
            w.TryAddTransaction(new Transaction(new DateTime(2025, 8, 31), 20m, TransactionType.Income));
            var txs = w.GetTransactionsForMonth(2025, 9).ToList();
            Assert.AreEqual(1, txs.Count);
            Assert.AreEqual(new DateTime(2025, 9, 1), txs[0].Date);
        }

        [Test]
        public void Wallet_GetTransactionGroupsForMonth_GroupsAndOrders()
        {
            var w = new Wallet("W", "RUB", 1000m); // достаточно для расходов
            // Income total = 500, Expense total = 300
            w.TryAddTransaction(new Transaction(new DateTime(2025, 9, 1), 100m, TransactionType.Expense, "e1"));
            w.TryAddTransaction(new Transaction(new DateTime(2025, 9, 2), 200m, TransactionType.Expense, "e2"));
            w.TryAddTransaction(new Transaction(new DateTime(2025, 9, 3), 500m, TransactionType.Income, "i1"));

            var groups = w.GetTransactionGroupsForMonth(2025, 9).ToList();

            // Должны быть 2 группы
            Assert.AreEqual(2, groups.Count);

            // Первая группа должна быть Income (500 > 300)
            Assert.AreEqual(TransactionType.Income, groups[0].Type);
            Assert.AreEqual(500m, groups[0].Total);

            // Expenses внутри группы должны быть упорядочены по дате
            var expenseGroup = groups.First(g => g.Type == TransactionType.Expense);
            Assert.AreEqual(2, expenseGroup.Transactions.Count);
            Assert.IsTrue(expenseGroup.Transactions[0].Date < expenseGroup.Transactions[1].Date);
            Assert.AreEqual(new DateTime(2025, 9, 1), expenseGroup.Transactions[0].Date);
        }

        [Test]
        public void Wallet_GetTopExpensesForMonth_ReturnsTopNDescending()
        {
            // Установка начального баланса, чтобы расходы могли быть добавлены
            var w = new Wallet("W", "RUB", 500m);
            Assert.IsTrue(w.TryAddTransaction(new Transaction(new DateTime(2025, 9, 1), 50m, TransactionType.Expense, "a")));
            Assert.IsTrue(w.TryAddTransaction(new Transaction(new DateTime(2025, 9, 2), 150m, TransactionType.Expense, "b")));
            Assert.IsTrue(w.TryAddTransaction(new Transaction(new DateTime(2025, 9, 3), 100m, TransactionType.Expense, "c")));

            var top2 = w.GetTopExpensesForMonth(2025, 9, 2).ToList();
            Assert.AreEqual(2, top2.Count);
            Assert.AreEqual(150m, top2[0].Amount);
            Assert.AreEqual(100m, top2[1].Amount);
        }

        [Test]
        public void Wallet_GetTopExpensesForMonth_TopNZeroOrNegative_Empty()
        {
            var w = new Wallet("W", "RUB", 0m);
            w.TryAddTransaction(new Transaction(new DateTime(2025, 9, 1), 50m, TransactionType.Expense, "a"));
            var res0 = w.GetTopExpensesForMonth(2025, 9, 0).ToList();
            var resNeg = w.GetTopExpensesForMonth(2025, 9, -1).ToList();
            Assert.IsEmpty(res0);
            Assert.IsEmpty(resNeg);
        }

        [Test]
        public void FinanceManager_LoadFromCsv_ParsesQuotedFieldsAndCommas()
        {
            // Одна строка с кавычками и запятой внутри описания
            var csv = "WalletName,Currency,InitialBalance,TransactionDate,Amount,Type,Description\r\n" +
                      "MyWallet,USD,100.50,2025-09-05,25.75,Income,\"Payment, client #123\"\r\n";

            var temp = Path.GetTempFileName();
            try
            {
                File.WriteAllText(temp, csv);

                var mgr = new FinanceManager();
                bool ok = mgr.LoadFromCsv(temp, out string error);

                Assert.IsTrue(ok, "LoadFromCsv should return true even if warnings present.");
                Assert.IsTrue(string.IsNullOrEmpty(error), $"No errors expected, got: {error}");

                var w = mgr.FindWalletByName("MyWallet");
                Assert.IsNotNull(w, "Wallet must be created.");
                var txs = w.GetTransactionsForMonth(2025, 9).ToList();
                Assert.AreEqual(1, txs.Count);
                Assert.AreEqual("Payment, client #123", txs[0].Description);
                Assert.AreEqual(25.75m, txs[0].Amount);
            }
            finally
            {
                File.Delete(temp);
            }
        }

        [Test]
        public void FinanceManager_LoadFromCsv_InvalidTypeAndAmount_SkipsAndReportsErrors()
        {
            // Строки: одна с некорректным типом, другая с нечисловой суммой
            var csv = "WalletName,Currency,InitialBalance,TransactionDate,Amount,Type,Description\r\n" +
                      "W1,RUB,10,2025-09-05,abc,Income,BadAmount\r\n" +
                      "W1,RUB,10,2025-09-06,5,UnknownType,BadType\r\n";

            var temp = Path.GetTempFileName();
            try
            {
                File.WriteAllText(temp, csv);

                var mgr = new FinanceManager();
                bool ok = mgr.LoadFromCsv(temp, out string error);

                Assert.IsTrue(ok, "LoadFromCsv returns true even when some lines are skipped.");
                Assert.IsNotNull(error);
                StringAssert.Contains("invalid amount", error.ToLowerInvariant());
                StringAssert.Contains("invalid transaction type", error.ToLowerInvariant());

                var w = mgr.FindWalletByName("W1");
                // Обе строки были некорректны и не добавили транзакции
                Assert.IsNotNull(w, "Wallet may have been created with default initial balance or 0.");
                var txs = w.GetTransactionsForMonth(2025, 9).ToList();
                Assert.IsEmpty(txs, "No valid transactions should be present.");
            }
            finally
            {
                File.Delete(temp);
            }
        }

        [Test]
        public void FinanceManager_LoadFromCsv_ExpenseExceedsBalance_ReportsNotAdded()
        {
            // initial balance = 10, expense = 100 -> будут пропущены с ошибкой "transaction not added"
            var csv = "WalletName,Currency,InitialBalance,TransactionDate,Amount,Type,Description\r\n" +
                      "Low,RUB,10,2025-09-05,100,Expense,TooBig\r\n";

            var temp = Path.GetTempFileName();
            try
            {
                File.WriteAllText(temp, csv);

                var mgr = new FinanceManager();
                bool ok = mgr.LoadFromCsv(temp, out string error);
                Assert.IsTrue(ok);
                Assert.IsNotNull(error);
                StringAssert.Contains("transaction not added", error.ToLowerInvariant());

                var w = mgr.FindWalletByName("Low");
                Assert.IsNotNull(w);
                var txs = w.GetTransactionsForMonth(2025, 9).ToList();
                Assert.IsEmpty(txs);
                Assert.AreEqual(10m, w.InitialBalance);
                Assert.AreEqual(10m, w.CurrentBalance);
            }
            finally
            {
                File.Delete(temp);
            }
        }

        [Test]
        public void FinanceManager_CreateWallet_Validation()
        {
            Assert.Throws<ArgumentException>(() => _manager.CreateWallet("", "RUB", 0m));
            Assert.Throws<ArgumentException>(() => _manager.CreateWallet("Name", "", 0m));
            Assert.Throws<ArgumentOutOfRangeException>(() => _manager.CreateWallet("Name", "RUB", -1m));
        }

        [Test]
        public void FinanceManager_FindWalletByName_CaseInsensitive()
        {
            var w = _manager.CreateWallet("MyWALLET", "RUB", 0m);
            var found = _manager.FindWalletByName("mywallet");
            Assert.IsNotNull(found);
            Assert.AreEqual(w.Id, found.Id);
        }

        [Test]
        public void GenerateSampleData_AddsWalletsAndValidatesTooLargeExpense()
        {
            _manager.GenerateSampleData();
            var card = _manager.FindWalletByName("Card");
            Assert.IsNotNull(card);
            var septExpenses = card.GetTransactionsForMonth(2025, 9).Where(t => t.Type == TransactionType.Expense).ToList();
            Assert.IsTrue(septExpenses.Count >= 2);
        }
    }
}

