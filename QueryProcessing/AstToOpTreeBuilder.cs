﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MetadataManager;
using Microsoft.FSharp.Core;
using PageManager;
using QueryProcessing.Exceptions;

namespace QueryProcessing
{
    public class AstToOpTreeBuilder
    {
        private MetadataManager.MetadataManager metadataManager;

        public AstToOpTreeBuilder(MetadataManager.MetadataManager metadataManager)
        {
            this.metadataManager = metadataManager;
        }


        public async Task<IPhysicalOperator<RowHolder>> ParseSqlStatement(Sql.sqlStatement sqlStatement, ITransaction tran)
        {
            // TODO: query builder is currently manual. i.e. SCAN -> optional(FILTER) -> PROJECT.
            // In future we need to build proper algebrizer, relational algebra rules and work on QO.
            string tableName = sqlStatement.Table;

            Sql.columnSelect[] columns = sqlStatement.Columns.ToArray();

            string[] projections = columns
                .Where(c => c.IsProjection == true)
                .Select(c => ((Sql.columnSelect.Projection)c).Item).ToArray();

            Tuple<Sql.aggType, string>[] aggregates = columns
                .Where(c => c.IsAggregate == true)
                .Select(c => ((Sql.columnSelect.Aggregate)c).Item).ToArray();

            MetadataTablesManager tableManager = metadataManager.GetTableManager();
            MetadataTable table = await tableManager.GetByName(tableName, tran).ConfigureAwait(false);

            // Scan Op.
            PhyOpScan scanOp = new PhyOpScan(table.Collection, tran);

            // Where op.
            IPhysicalOperator<RowHolder> sourceForProject = scanOp;

            if (FSharpOption<Sql.where>.get_IsSome(sqlStatement.Where))
            {
                Sql.where whereStatement = sqlStatement.Where.Value;
                PhyOpFilter filterOp = new PhyOpFilter(scanOp, FilterStatementBuilder.EvalWhere(whereStatement, table.Columns));
                sourceForProject = filterOp;
            }

            if (sqlStatement.GroupBy.Any() || aggregates.Any())
            {
                string[] groupByColumns = sqlStatement.GroupBy.ToArray();

                GroupByFunctors groupByFunctors = GroupByStatementBuilder.EvalGroupBy(groupByColumns, columns, table.Columns);
                PhyOpGroupBy phyOpGroupBy = new PhyOpGroupBy(sourceForProject, groupByFunctors);

                return phyOpGroupBy;
            } else
            {
                // Project Op.
                List<int> columnMapping = new List<int>();
                foreach (string columnName in projections)
                {
                    if (!table.Columns.Any(tbl => tbl.ColumnName == columnName))
                    {

                        throw new KeyNotFoundException(string.Format("Invalid column name {0}", columnName));
                    }

                    columnMapping.Add(table.Columns.FirstOrDefault(c => c.ColumnName == columnName).ColumnId);
                }

                PhyOpProject projectOp = new PhyOpProject(sourceForProject, columnMapping.ToArray());

                return projectOp;
            }
        }

        public async Task<PhyOpTableInsert> ParseInsertStatement(Sql.insertStatement insertStatement, ITransaction tran)
        {
            string tableName = insertStatement.Table;

            MetadataTablesManager tableManager = metadataManager.GetTableManager();
            MetadataTable table = await tableManager.GetByName(tableName, tran).ConfigureAwait(false);

            ColumnInfo[] columnInfosFromTable = table.Columns.Select(mt => mt.ColumnType).ToArray();

            RowHolder rowHolder = new RowHolder(columnInfosFromTable);

            int colNum = 0;
            foreach (var value in insertStatement.Values)
            {
                if (value.IsFloat)
                {
                    if (columnInfosFromTable[colNum].ColumnType == ColumnType.Double)
                    {
                        rowHolder.SetField<double>(colNum, ((Sql.value.Float)value).Item);
                    }
                    else
                    {
                        throw new InvalidColumnTypeException();
                    }
                }
                else if (value.IsInt)
                {
                    if (columnInfosFromTable[colNum].ColumnType == ColumnType.Int)
                    {
                        rowHolder.SetField<int>(colNum, ((Sql.value.Int)value).Item);
                    }
                    else if (columnInfosFromTable[colNum].ColumnType == ColumnType.Double)
                    {
                        // Int can be cast to double without data loss.
                        rowHolder.SetField<double>(colNum, (double)((Sql.value.Int)value).Item);
                    }
                    else
                    {
                        throw new InvalidColumnTypeException();
                    }
                }
                else if (value.IsString)
                {
                    if (columnInfosFromTable[colNum].ColumnType == ColumnType.String)
                    {
                        // TODO: For string heap (strings of variable length separate logic is needed.
                        rowHolder.SetField(colNum, ((Sql.value.String)value).Item.ToCharArray());
                    }
                    else
                    {
                        throw new InvalidColumnTypeException();
                    }
                }
                else { throw new ArgumentException(); }

                colNum++;
            }

            PhyOpStaticRowProvider opStatic = new PhyOpStaticRowProvider(rowHolder);

            PhyOpTableInsert op = new PhyOpTableInsert(table.Collection, opStatic, tran);
            return op;
        }
    }
}
