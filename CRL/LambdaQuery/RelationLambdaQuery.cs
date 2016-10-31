/**
* CRL 快速开发框架 V4.0
* Copyright (c) 2016 Hubro All rights reserved.
* GitHub https://github.com/hubro-xx/CRL3
* 主页 http://www.cnblogs.com/hubro
* 在线文档 http://crl.changqidongli.com/
*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CRL.LambdaQuery
{
    public sealed partial class RelationLambdaQuery<T> : LambdaQuery<T> where T : IModel, new()
    {
        /// <summary>
        /// lambda查询
        /// </summary>
        /// <param name="_dbContext"></param>
        /// <param name="_useTableAliasesName">查询是否生成表别名,在更新和删除时用</param>
        public RelationLambdaQuery(DbContext _dbContext, bool _useTableAliasesName = true)
            : base(_dbContext, _useTableAliasesName)
        {
           
        }

        /// <summary>
        /// 设置条件 可累加，按and
        /// </summary>
        /// <param name="expression">最好用变量代替属性或方法</param>
        /// <returns></returns>
        public override LambdaQuery<T> Where(Expression<Func<T, bool>> expression)
        {
            if (expression == null)
                return this;
            //var b = expression.ToString();
            //if (QueryFields.Count == 0)
            //{
            //    SelectAll();
            //}
            string condition = FormatExpression(expression.Body).SqlOut;
            if (Condition.Length > 0)
            {
                condition = " and " + condition;
            }
            Condition.Append(condition);
            //this.Condition += string.IsNullOrEmpty(Condition) ? condition : " and " + condition;
            return this;
        }
        /// <summary>
        /// 设置排序 可累加
        /// </summary>
        /// <param name="expression"></param>
        /// <param name="desc">是否倒序</param>
        /// <returns></returns>
        public override LambdaQuery<T> OrderBy<TResult>(Expression<Func<T, TResult>> expression, bool desc = true)
        {
            var fields = GetSelectField(false, expression.Body, false, typeof(T));
            if (!string.IsNullOrEmpty(__QueryOrderBy))
            {
                __QueryOrderBy += ",";
            }
            __QueryOrderBy += string.Format(" {0} {1}", fields.First().QueryField, desc ? "desc" : "asc");
            __QueryOrderBy = ReplacePrefix(__QueryOrderBy);
            return this;
        }
        /// <summary>
        /// 按主键排序
        /// </summary>
        /// <param name="desc"></param>
        /// <returns></returns>
        public override LambdaQuery<T> OrderByPrimaryKey(bool desc)
        {
            if (!string.IsNullOrEmpty(__QueryOrderBy))
            {
                __QueryOrderBy += ",";
            }
            var key = TypeCache.GetTable(typeof(T)).PrimaryKey;
            __QueryOrderBy += string.Format(" {2}{0} {1}", key.MapingName, desc ? "desc" : "asc", GetPrefix());
            //QueryOrderBy = ReplacePrefix(QueryOrderBy);
            return this;
        }
        /// <summary>
        /// 按当前条件累加OR条件
        /// </summary>
        /// <param name="expression"></param>
        /// <returns></returns>
        public override LambdaQuery<T> Or(Expression<Func<T, bool>> expression)
        {
            string condition1 = FormatExpression(expression.Body).SqlOut;
            //this.Condition = string.Format("({0}) or {1}", Condition, condition1);
            Condition.Append(string.Format(" or {0}", condition1));
            return this;
        }
        protected override LambdaQuery<T> InnerSelect<TInner>(Expression<Func<T, object>> outField, Expression<Func<TInner, object>> innerField,
    Expression<Func<T, TInner, bool>> expression, string type)
        {
            MemberExpression m1 = null, m2;
            //object 会生成UnaryExpression表达式 Convert(b=>b.UserId)
            if (outField != null)//兼容exists 可能为空
            {
                if (outField.Body is UnaryExpression)
                {
                    m1 = (outField.Body as UnaryExpression).Operand as MemberExpression;
                }
                else
                {
                    m1 = outField.Body as MemberExpression;
                }
            }
            if (innerField.Body is UnaryExpression)
            {
                m2 = (innerField.Body as UnaryExpression).Operand as MemberExpression;
            }
            else
            {
                m2 = innerField.Body as MemberExpression;
            }
            string field1 = "";
            if (outField != null)
            {
                field1 = string.Format("{0}{1}", GetPrefix(), __DBAdapter.KeyWordFormat(m1.Member.Name));
            }
            string field2 = string.Format("{0}{1}", GetPrefix(typeof(TInner)), __DBAdapter.KeyWordFormat(m2.Member.Name));
            //var visitor2 = new ExpressionVisitor<TInner>(dbContext);
            //string condition = visitor2.RouteExpressionHandler(expression.Body);
            string condition = FormatJoinExpression(expression.Body);

            condition = string.Format(condition, GetPrefix(typeof(TInner)), "");
            var tableName = TypeCache.GetTableName(typeof(TInner), __DbContext);
            tableName = tableName + " " + GetPrefix(typeof(TInner));
            tableName = tableName.Substring(0, tableName.Length - 1);
            condition = string.Format("{0} {1}(select {2} from {3} where {4})", field1, type, field2, tableName + __DBAdapter.GetWithNolockFormat(), condition);
            //this.Condition += string.IsNullOrEmpty(Condition) ? condition : " and " + condition;
            if (Condition.Length > 0)
            {
                condition = " and " + condition;
            }
            Condition.Append(condition);
            return this;
        }

        internal override string GetQueryFieldString()
        {
            if (__QueryFields.Count == 0)
            {
                SelectAll();
            }
            int tabIndex = 2;
            var sb = new StringBuilder();
            foreach (Attribute.FieldAttribute a in __QueryFields)
            {
                if (a.FieldType == Attribute.FieldType.关联字段)
                {
                    #region 关联约束
                    tabIndex += 1;
                    if (a.FieldType == Attribute.FieldType.关联字段 && a.ConstraintType == null)//虚拟字段,没有设置关联类型
                    {
                        throw new CRLException(string.Format("需指定关联类型:{0}.{1}.Attribute.Field.ConstraintType", typeof(T), a.MemberName));
                    }
                    if (string.IsNullOrEmpty(a.ConstraintField))//约束为空
                    {
                        continue;
                    }
                    var arry = a.ConstraintField.Replace("$", "").Split('=');
                    string leftField = GetPrefix() + arry[0];
                    var innerType = a.ConstraintType;
                    //TypeCache.SetDBAdapterCache(innerType,dBAdapter);
                    string rightField = GetPrefix(innerType) + arry[1];
                    string condition = string.Format("{0}={1}", leftField, rightField);
                    if (!string.IsNullOrEmpty(a.Constraint))
                    {
                        a.Constraint = Regex.Replace(a.Constraint, @"(.+?)\=", GetPrefix(innerType) + "$1=");//加上前缀
                        condition += " and " + a.Constraint;
                    }

                    var innerFields = TypeCache.GetProperties(innerType, true);

                    //var resultField = innerFields.Find(b => b.Name.ToUpper() == a.ConstraintResultField.ToUpper());
                    var resultField = innerFields[a.ConstraintResultField];
                    if (resultField == null)
                    {
                        throw new CRLException(string.Format("在类型{0}找不到 ConstraintResultField {1}", innerType, a.ConstraintResultField));
                    }
                    AddInnerRelation(innerType, condition);
                    #endregion
                    sb.Append(string.Format("{0},", resultField.QueryFullScript));
                }
                else
                {
                    sb.Append(string.Format("{0},", a.QueryFullScript));
                }
            }
            var str2 = sb.ToString().Substring(0, sb.Length - 1);
            return str2;
        }

        /// <summary>
        /// 获取查询条件串,带表名
        /// </summary>
        /// <returns></returns>
        internal override string GetQueryConditions(bool withTableName = true)
        {
            var where = Condition;
            #region group判断
            if (__GroupFields.Count > 0)
            {
                where.Append(" group by ");
                where.Append(string.Join(",", __GroupFields.Select(b => b.QueryField)));
            }
            if (!string.IsNullOrEmpty(Having))
            {
                where.Append(" having " + Having);
            }
            #endregion
            StringBuilder part = new StringBuilder();
            if (withTableName)
            {
                part.Append(string.Format("{0} t1 {1}", __DBAdapter.KeyWordFormat(QueryTableName), __DBAdapter.GetWithNolockFormat()));
            }
            if (_IsRelationUpdate)
            {
                if (where.Length > 0)
                {
                    part.Append(string.Format(" where {0}", where));
                }
            }
            else
            {
                string join = string.Join(" ", __Relations.Values);
                part.Append(string.Format(" {0}{1}", join, where.Length == 0 ? " " : " where " + where));
            }
            return part.ToString();
        }
        /// <summary>
        /// 获取排序 带 order by
        /// </summary>
        /// <returns></returns>
        internal override string GetOrderBy()
        {
            string orderBy = __QueryOrderBy;
            if (string.IsNullOrEmpty(orderBy))
            {
                orderBy = TypeCache.GetTable(typeof(T)).DefaultSort;
            }
            orderBy = string.IsNullOrEmpty(orderBy) ? orderBy : " order by " + orderBy;
            orderBy = ReplacePrefix(orderBy);
            return orderBy;
        }
        /// <summary>
        /// 获取完整查询
        /// </summary>
        /// <returns></returns>
        internal override string GetQuery()
        {
            string fields = GetQueryFieldString();
            if (!string.IsNullOrEmpty(__FieldFunctionFormat))
            {
                fields = string.Format(__FieldFunctionFormat, fields);
            }
            if (distinctCount)
            {
                fields = System.Text.RegularExpressions.Regex.Replace(fields,@" as \w+"," ");//替换别名
                fields = string.Format(" count({0}) as Total", fields);
                if (__QueryFields.Count > 1)
                {
                    throw new CRLException("distinct 时,不能count多个字段 " + fields);
                }
            }

            var part = " from " + GetQueryConditions();

            var orderBy = GetOrderBy();
            StringBuilder sql = new StringBuilder();
            //当设置了分表关联
            if (__DbContext.UseSharding && __UnionType != Sharding.UnionType.None)
            {
                string tableName = TypeCache.GetTable(typeof(T)).TableName;
                var tables = Sharding.DBService.GetAllTable(__DbContext.DBLocation.ShardingDataBase, tableName);
                string union = __UnionType == Sharding.UnionType.Union ? "union" : "union all";
                //var dbExtend = new DBExtend(dbContext); //todo 检查分表是否被创建
                for (int i = 0; i < tables.Count; i++)
                {
                    var table = tables[i];
                    var part1 = part.Replace("from " + __DBAdapter.KeyWordFormat(tableName), "from " + __DBAdapter.KeyWordFormat(table.PartName));
                    sql.Append(__DBAdapter.GetSelectTop(fields, part1, "", TakeNum));
                    if (i < tables.Count - 1)
                    {
                        sql.Append("\r\n" + union + "\r\n");
                    }
                }
                sql.Append(orderBy);
            }
            else
            {
                var sql2 = __DBAdapter.GetSelectTop(fields, part, orderBy, TakeNum);
                sql.Append(sql2);
            }
            var ts = DateTime.Now - startTime;
            AnalyticalTime = ts.TotalMilliseconds;
            return sql.ToString();
        }
    }
}
