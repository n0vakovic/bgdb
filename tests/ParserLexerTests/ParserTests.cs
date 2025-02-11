using NUnit.Framework;
using FSharp.Text.Lexing;
using Microsoft.FSharp.Core;
using System;
using System.Linq;

namespace ParserLexerTests
{
    public class ParsingTests
    {
        [Test]
        public void ParsingExtractTest()
        {
            string query =
                @"SELECT x, y, z   
                FROM t1   
                LEFT JOIN t2   
                INNER JOIN t3 ON t3.ID = t2.ID   
                WHERE x = 50 AND y = 20   
                ORDER BY x ASC, y DESC, z";

            var selectStatement = GetSelectStatement(query);

            Assert.AreEqual(
                new string[] { "x", "y", "z" },
                (((Sql.selectType.ColumnList)selectStatement.Columns).Item).Select(c => ((Sql.columnSelect.Projection)c).Item).ToArray());
        }

        [Test]
        public void ParsingWhereClause()
        {
            string query =
                @"SELECT x, y, z   
                FROM t1   
                WHERE x = 50 AND y = 20";

            var selectStatement = GetSelectStatement(query);

            Assert.AreEqual(
                new string[] { "x", "y", "z" },
                (((Sql.selectType.ColumnList)selectStatement.Columns).Item).Select(c => ((Sql.columnSelect.Projection)c).Item).ToArray());
            Assert.AreEqual("t1", selectStatement.Table);

            Sql.where whereStatement = selectStatement.Where.Value;
            Assert.IsTrue(whereStatement.IsAnd);

            Sql.where.And andStatement = (Sql.where.And)whereStatement;
            Assert.IsTrue(andStatement.Item1.IsCond);
            Assert.IsTrue(andStatement.Item2.IsCond);

            Sql.where.Cond leftCond = (Sql.where.Cond)andStatement.Item1;
            Sql.where.Cond rightCond = (Sql.where.Cond)andStatement.Item2;

            Assert.IsTrue(leftCond.Item.Item1.IsId);
            Assert.IsTrue(((Sql.value.Id)leftCond.Item.Item1).Item == "x");
            Assert.IsTrue(leftCond.Item.Item2.IsEq);
            Assert.IsTrue(leftCond.Item.Item3.IsInt);
            Assert.IsTrue(((Sql.value.Int)leftCond.Item.Item3).Item == 50);

            Assert.IsTrue(rightCond.Item.Item1.IsId);
            Assert.IsTrue(((Sql.value.Id)rightCond.Item.Item1).Item == "y");
            Assert.IsTrue(rightCond.Item.Item2.IsEq);
            Assert.IsTrue(rightCond.Item.Item3.IsInt);
            Assert.IsTrue(((Sql.value.Int)rightCond.Item.Item3).Item == 20);
        }

        [Test]
        public void CreateTableTest()
        {
            string query = "CREATE TABLE mytable (TYPE_INT A, TYPE_INT B, TYPE_STRING(10) C)";

            var createStatement = GetCreateTableStatement(query);

            Assert.AreEqual("mytable", createStatement.Table);
            Assert.IsTrue(createStatement.ColumnList[0].Item1.IsIntCType);
            Assert.IsTrue(createStatement.ColumnList[1].Item1.IsIntCType);
            Assert.IsTrue(createStatement.ColumnList[2].Item1.IsStringCType);
            Assert.AreEqual(10, createStatement.ColumnList[2].Item2);
            Assert.AreEqual(new string[] { "A", "B", "C" }, createStatement.ColumnList.Select(cl => cl.Item3).ToArray());
        }

        [Test]
        public void InsertStatementTests()
        {
            string query = "INSERT INTO mytable VALUES (17,'11','TST')";

            var insertStatement = GetInsertStatement(query);

            Assert.AreEqual("mytable", insertStatement.Table);
            Assert.IsTrue(insertStatement.Values[0].IsInt);
            Assert.IsTrue(insertStatement.Values[1].IsString);
            Assert.IsTrue(insertStatement.Values[2].IsString);

            Assert.AreEqual(((Sql.value.Int)insertStatement.Values[0]).Item, 17);
            Assert.AreEqual(((Sql.value.String)insertStatement.Values[1]).Item, "11");
            Assert.AreEqual(((Sql.value.String)insertStatement.Values[2]).Item, "TST");
        }

        [Test]
        public void GroupByStatementTest()
        {
            string query =
                @"SELECT x, y, z   
                FROM t1   
                GROUP BY z, y, x";

            var selectStatement = GetSelectStatement(query);

            Assert.AreEqual(new string[] { "x", "y", "z" },
                (((Sql.selectType.ColumnList)selectStatement.Columns).Item).Select(c => ((Sql.columnSelect.Projection)c).Item).ToArray());
            Assert.AreEqual("t1", selectStatement.Table);

            string[] groupBys = selectStatement.GroupBy.ToArray();
            Assert.AreEqual(new[] { "z", "y", "x" }, groupBys);
        }

        [Test]
        public void AggregateStatementTest()
        {
            string query =
                @"SELECT MAX(x), MIN(y), z   
                FROM t1   
                GROUP BY z";

            var selectStatement = GetSelectStatement(query);

            var columns = (((Sql.selectType.ColumnList)selectStatement.Columns).Item);

            Assert.AreEqual("x", ((Sql.columnSelect.Aggregate)columns[0]).Item.Item2);
            Assert.AreEqual(Sql.aggType.Max ,((Sql.columnSelect.Aggregate)columns[0]).Item.Item1);

            Assert.AreEqual("y", ((Sql.columnSelect.Aggregate)columns[1]).Item.Item2);
            Assert.AreEqual(Sql.aggType.Min,((Sql.columnSelect.Aggregate)columns[1]).Item.Item1);

            Assert.AreEqual("z", ((Sql.columnSelect.Projection)columns[2]).Item);
        }

        [Test]
        public void StartSelectTest()
        {
            string query = "SELECT * FROM t1";

            var selectStatement = GetSelectStatement(query);
            Assert.IsTrue(selectStatement.Columns.IsStar);
        }

        [Test]
        public void TopStatement()
        {
            string query = "SELECT TOP 10 * FROM t1";

            var selectStatement = GetSelectStatement(query);
            Assert.IsTrue(selectStatement.Columns.IsStar);

            Assert.IsTrue(FSharpOption<int>.get_IsSome(selectStatement.Top));
            Assert.AreEqual(10, selectStatement.Top.Value);
        }

        [Test]
        public void DotInNameTest()
        {
            string query =
                @"SELECT MAX(t1.x), MIN(t1.y), t1.z
                FROM t1
                GROUP BY t1.z";

            var selectStatement = GetSelectStatement(query);

            var columns = (((Sql.selectType.ColumnList)selectStatement.Columns).Item);

            Assert.AreEqual("t1.x", ((Sql.columnSelect.Aggregate)columns[0]).Item.Item2);
            Assert.AreEqual(Sql.aggType.Max ,((Sql.columnSelect.Aggregate)columns[0]).Item.Item1);

            Assert.AreEqual("t1.y", ((Sql.columnSelect.Aggregate)columns[1]).Item.Item2);
            Assert.AreEqual(Sql.aggType.Min,((Sql.columnSelect.Aggregate)columns[1]).Item.Item1);

            Assert.AreEqual("t1.z", ((Sql.columnSelect.Projection)columns[2]).Item);
        }

        [Test]
        public void JoinTest()
        {
            string query = @"
SELECT t1.a, t2.b, t3.c
FROM t1
JOIN t2 ON t1.a = t2.b
JOIN t3
WHERE t1.a > 20
";
            var selectStatement = GetSelectStatement(query);

            Assert.AreEqual(2, selectStatement.Joins.Length);

            Assert.AreEqual("t2", selectStatement.Joins[0].Item1);
            Assert.AreEqual(Sql.joinType.Inner, selectStatement.Joins[0].Item2);
            Assert.IsTrue(FSharpOption<Sql.where>.get_IsSome(selectStatement.Joins[0].Item3));
            Sql.where whereStatement = selectStatement.Joins[0].Item3.Value;
            Assert.IsTrue(whereStatement.IsCond);
            Sql.where.Cond whereCond = (Sql.where.Cond)whereStatement;
            Assert.IsTrue(whereCond.Item.Item1.IsId);
            Assert.IsTrue(whereCond.Item.Item3.IsId);

            Assert.AreEqual("t3", selectStatement.Joins[1].Item1);
            Assert.AreEqual(Sql.joinType.Inner, selectStatement.Joins[1].Item2);
            Assert.IsFalse(FSharpOption<Sql.where>.get_IsSome(selectStatement.Joins[1].Item3));
        }

        [Test]
        public void OrderByTest()
        {
            string query = "SELECT a, b, c FROM t ORDER BY a";
            var statement = GetSelectStatement(query);

            Tuple<string, Sql.dir>[] values = statement.OrderBy.ToArray();

            Assert.AreEqual(1, values.Length);
            Assert.AreEqual("a", values[0].Item1);
            Assert.AreEqual(Sql.dir.Asc, values[0].Item2);
        }

        [Test]
        public void OrderByDescTest()
        {
            string query = "SELECT a, b, c FROM t ORDER BY a DESC";
            var statement = GetSelectStatement(query);

            Tuple<string, Sql.dir>[] values = statement.OrderBy.ToArray();

            Assert.AreEqual(1, values.Length);
            Assert.AreEqual("a", values[0].Item1);
            Assert.AreEqual(Sql.dir.Desc, values[0].Item2);
        }

        [Test]
        public void InvalidInputTest()
        {
            string query = "INVALID INPUT STRING";
            Assert.Throws<Exception>(() => GetSqlStatement(query), "parse error");
        }

        #region Helper
        private Sql.sqlStatement GetSelectStatement(string query)
        {
            Sql.DmlDdlSqlStatement statement = GetSqlStatement(query);
            Assert.IsTrue(statement.IsSelect);

            return ((Sql.DmlDdlSqlStatement.Select)statement).Item;
        }

        private Sql.insertStatement GetInsertStatement(string query)
        {
            Sql.DmlDdlSqlStatement statement = GetSqlStatement(query);
            Assert.IsTrue(statement.IsInsert);

            return ((Sql.DmlDdlSqlStatement.Insert)statement).Item;
        }

        private Sql.createTableStatement GetCreateTableStatement(string query)
        {
            Sql.DmlDdlSqlStatement statement = GetSqlStatement(query);
            Assert.IsTrue(statement.IsCreate);

            return ((Sql.DmlDdlSqlStatement.Create)statement).Item;
        }

        private static Sql.DmlDdlSqlStatement GetSqlStatement(string query)
        {
            var lexbuf = LexBuffer<char>.FromString(query);
            Func<LexBuffer<char>, SqlParser.token> func = (x) => SqlLexer.tokenize(x);

            var f = FuncConvert.FromFunc(func);
            Sql.DmlDdlSqlStatement statement = SqlParser.startCT(FuncConvert.FromFunc(func), lexbuf);
            return statement;
        }
        #endregion Helper
    }
}