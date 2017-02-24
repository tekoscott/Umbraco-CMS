﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Dynamic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using Umbraco.Core.Models;
using Umbraco.Core;
using Umbraco.Core.Models.EntityBase;
using Umbraco.Core.Models.Rdbms;
using Umbraco.Core.Persistence.Factories;
using Umbraco.Core.Persistence.Querying;
using Umbraco.Core.Persistence.UnitOfWork;
using Umbraco.Core.Strings;

namespace Umbraco.Core.Persistence.Repositories
{
    /// <summary>
    /// Represents the EntityRepository used to query <see cref="IUmbracoEntity"/> objects.
    /// </summary>
    /// <remarks>
    /// This is limited to objects that are based in the umbracoNode-table.
    /// </remarks>
    internal class EntityRepository : DisposableObject, IEntityRepository
    {
        private readonly IDatabaseUnitOfWork _work;

        public EntityRepository(IDatabaseUnitOfWork work)
		{
		    _work = work;
		}

        /// <summary>
        /// Returns the Unit of Work added to the repository
        /// </summary>
        protected internal IDatabaseUnitOfWork UnitOfWork
        {
            get { return _work; }
        }

        /// <summary>
        /// Internal for testing purposes
        /// </summary>
        internal Guid UnitKey
        {
            get { return (Guid)_work.Key; }
        }

        #region Query Methods

        public IUmbracoEntity GetByKey(Guid key)
        {
            var sql = GetBaseWhere(GetBase, false, false, key);
            var nodeDto = _work.Database.FirstOrDefault<dynamic>(sql);
            if (nodeDto == null)
                return null;

            var factory = new UmbracoEntityFactory();
            var entity = factory.BuildEntityFromDynamic(nodeDto);

            return entity;
        }

        public IUmbracoEntity GetByKey(Guid key, Guid objectTypeId)
        {
            bool isContent = objectTypeId == new Guid(Constants.ObjectTypes.Document);
            bool isMedia = objectTypeId == new Guid(Constants.ObjectTypes.Media);

            var sql = GetFullSqlForEntityType(key, isContent, isMedia, objectTypeId);

            var factory = new UmbracoEntityFactory();

            if (isMedia)
            {
                //for now treat media differently and include all property data too  
                var entities = _work.Database.Fetch<dynamic, UmbracoPropertyDto, UmbracoEntity>(
                    new UmbracoEntityRelator().Map, sql);

                return entities.FirstOrDefault();
            }
            else
            {

                //query = read forward data reader, do not load everything into mem
                var dtos = _work.Database.Query<dynamic>(sql);
                var collection = new EntityDefinitionCollection();
                foreach (var dto in dtos)
                {
                    collection.AddOrUpdate(new EntityDefinition(factory, dto, isContent, false));
                }
                var found = collection.FirstOrDefault();
                return found != null ? found.BuildFromDynamic() : null;                
            }
            
            
        }

        public virtual IUmbracoEntity Get(int id)
        {
            var sql = GetBaseWhere(GetBase, false, false, id);
            var nodeDto = _work.Database.FirstOrDefault<dynamic>(sql);
            if (nodeDto == null)
                return null;

            var factory = new UmbracoEntityFactory();
            var entity = factory.BuildEntityFromDynamic(nodeDto);

            return entity;
        }

        public virtual IUmbracoEntity Get(int id, Guid objectTypeId)
        {
            bool isContent = objectTypeId == new Guid(Constants.ObjectTypes.Document);
            bool isMedia = objectTypeId == new Guid(Constants.ObjectTypes.Media);

            var sql = GetFullSqlForEntityType(id, isContent, isMedia, objectTypeId);

            var factory = new UmbracoEntityFactory();

            if (isMedia)
            {
                //for now treat media differently and include all property data too  
                var entities = _work.Database.Fetch<dynamic, UmbracoPropertyDto, UmbracoEntity>(
                    new UmbracoEntityRelator().Map, sql);

                return entities.FirstOrDefault();
            }
            else
            {                
                //query = read forward data reader, do not load everything into mem
                var dtos = _work.Database.Query<dynamic>(sql);
                var collection = new EntityDefinitionCollection();
                foreach (var dto in dtos)
                {
                    collection.AddOrUpdate(new EntityDefinition(factory, dto, isContent, false));
                }
                var found = collection.FirstOrDefault();
                return found != null ? found.BuildFromDynamic() : null;
            }
        }

        public virtual IEnumerable<IUmbracoEntity> GetAll(Guid objectTypeId, params int[] ids)
        {
            if (ids.Any())
            {
                return PerformGetAll(objectTypeId, sql1 => sql1.Where(" umbracoNode.id in (@ids)", new {ids = ids}));
            }
            else
            {
                return PerformGetAll(objectTypeId);
            }
        }

        public virtual IEnumerable<IUmbracoEntity> GetAll(Guid objectTypeId, params Guid[] keys)
        {
            if (keys.Any())
            {
                return PerformGetAll(objectTypeId, sql1 => sql1.Where(" umbracoNode.uniqueID in (@keys)", new { keys = keys }));
            }
            else
            {
                return PerformGetAll(objectTypeId);
            }
        }

        private IEnumerable<IUmbracoEntity> PerformGetAll(Guid objectTypeId, Action<Sql> filter = null)
        {
            bool isContent = objectTypeId == new Guid(Constants.ObjectTypes.Document);
            bool isMedia = objectTypeId == new Guid(Constants.ObjectTypes.Media);
            var sql = GetFullSqlForEntityType(isContent, isMedia, objectTypeId, filter);

            var factory = new UmbracoEntityFactory();

            if (isMedia)
            {
                //for now treat media differently and include all property data too                
                var entities = _work.Database.Fetch<dynamic, UmbracoPropertyDto, UmbracoEntity>(
                    new UmbracoEntityRelator().Map, sql);
                return entities;
            }
            else
            {
                //query = read forward data reader, do not load everything into mem
                var dtos = _work.Database.Query<dynamic>(sql);
                var collection = new EntityDefinitionCollection();
                foreach (var dto in dtos)
                {
                    collection.AddOrUpdate(new EntityDefinition(factory, dto, isContent, false));
                }
                return collection.Select(x => x.BuildFromDynamic()).ToList();                
            }
        }


        public virtual IEnumerable<IUmbracoEntity> GetByQuery(IQuery<IUmbracoEntity> query)
        {
            var sqlClause = GetBase(false, false, null);
            var translator = new SqlTranslator<IUmbracoEntity>(sqlClause, query);
            var sql = translator.Translate().Append(GetGroupBy(false, false));

            var dtos = _work.Database.Fetch<dynamic>(sql);

            var factory = new UmbracoEntityFactory();
            var list = dtos.Select(factory.BuildEntityFromDynamic).Cast<IUmbracoEntity>().ToList();

            return list;
        }

        public virtual IEnumerable<IUmbracoEntity> GetByQuery(IQuery<IUmbracoEntity> query, Guid objectTypeId)
        {

            bool isContent = objectTypeId == new Guid(Constants.ObjectTypes.Document);
            bool isMedia = objectTypeId == new Guid(Constants.ObjectTypes.Media);

            var sqlClause = GetBaseWhere(GetBase, isContent, isMedia, null, objectTypeId);
            
            var translator = new SqlTranslator<IUmbracoEntity>(sqlClause, query);
            var entitySql = translator.Translate();

            var factory = new UmbracoEntityFactory();

            if (isMedia)
            {
                var wheres = query.GetWhereClauses().ToArray();

                var mediaSql = GetFullSqlForMedia(entitySql.Append(GetGroupBy(isContent, true, false)), sql =>
                {
                    //adds the additional filters
                    foreach (var whereClause in wheres)
                    {
                        sql.Where(whereClause.Item1, whereClause.Item2);
                    }
                });

                //for now treat media differently and include all property data too    
                var entities = _work.Database.Fetch<dynamic, UmbracoPropertyDto, UmbracoEntity>(
                    new UmbracoEntityRelator().Map, mediaSql);
                return entities;
            }
            else
            {
                //use dynamic so that we can get ALL properties from the SQL so we can chuck that data into our AdditionalData
                var finalSql = entitySql.Append(GetGroupBy(isContent, false));

                //query = read forward data reader, do not load everything into mem
                var dtos = _work.Database.Query<dynamic>(finalSql);
                var collection = new EntityDefinitionCollection();
                foreach (var dto in dtos)
                {
                    collection.AddOrUpdate(new EntityDefinition(factory, dto, isContent, false));
                }
                return collection.Select(x => x.BuildFromDynamic()).ToList();
            }
        }

        #endregion
        

        #region Sql Statements

        protected Sql GetFullSqlForEntityType(Guid key, bool isContent, bool isMedia, Guid objectTypeId)
        {
            var entitySql = GetBaseWhere(GetBase, isContent, isMedia, objectTypeId, key);

            if (isMedia == false) return entitySql.Append(GetGroupBy(isContent, false));

            return GetFullSqlForMedia(entitySql.Append(GetGroupBy(isContent, true, false)));
        }

        protected Sql GetFullSqlForEntityType(int id, bool isContent, bool isMedia, Guid objectTypeId)
        {
            var entitySql = GetBaseWhere(GetBase, isContent, isMedia, objectTypeId, id);

            if (isMedia == false) return entitySql.Append(GetGroupBy(isContent, false));

            return GetFullSqlForMedia(entitySql.Append(GetGroupBy(isContent, true, false)));
        }

        protected Sql GetFullSqlForEntityType(bool isContent, bool isMedia, Guid objectTypeId, Action<Sql> filter)
        {
            var entitySql = GetBaseWhere(GetBase, isContent, isMedia, filter, objectTypeId);

            if (isMedia == false) return entitySql.Append(GetGroupBy(isContent, false));

            return GetFullSqlForMedia(entitySql.Append(GetGroupBy(isContent, true, false)), filter);
        }

        private Sql GetFullSqlForMedia(Sql entitySql, Action<Sql> filter = null)
        {
            //this will add any dataNvarchar property to the output which can be added to the additional properties

            var joinSql = new Sql()
                .Select("contentNodeId, versionId, dataNvarchar, dataNtext, propertyEditorAlias, alias as propertyTypeAlias")
                .From<PropertyDataDto>()
                .InnerJoin<NodeDto>()
                .On<PropertyDataDto, NodeDto>(dto => dto.NodeId, dto => dto.NodeId)
                .InnerJoin<PropertyTypeDto>()
                .On<PropertyTypeDto, PropertyDataDto>(dto => dto.Id, dto => dto.PropertyTypeId)
                .InnerJoin<DataTypeDto>()
                .On<PropertyTypeDto, DataTypeDto>(dto => dto.DataTypeId, dto => dto.DataTypeId)
                .Where("umbracoNode.nodeObjectType = @nodeObjectType", new {nodeObjectType = Constants.ObjectTypes.Media});

            if (filter != null)
            {
                filter(joinSql);
            }

            //We're going to create a query to query against the entity SQL 
            // because we cannot group by nText columns and we have a COUNT in the entitySql we cannot simply left join
            // the entitySql query, we have to join the wrapped query to get the ntext in the result
            
            var wrappedSql = new Sql("SELECT * FROM (")
                .Append(entitySql)
                .Append(new Sql(") tmpTbl LEFT JOIN ("))
                .Append(joinSql)
                .Append(new Sql(") as property ON id = property.contentNodeId"))
                .OrderBy("sortOrder, id");

            return wrappedSql;
        }

        protected virtual Sql GetBase(bool isContent, bool isMedia, Action<Sql> customFilter)
        {
            var columns = new List<object>
            {
                "umbracoNode.id",
                "umbracoNode.trashed",
                "umbracoNode.parentID",
                "umbracoNode.nodeUser",
                "umbracoNode.level",
                "umbracoNode.path",
                "umbracoNode.sortOrder",
                "umbracoNode.uniqueID",
                "umbracoNode.text",
                "umbracoNode.nodeObjectType",
                "umbracoNode.createDate",
                "COUNT(parent.parentID) as children"
            };

            if (isContent || isMedia)
            {
                if (isContent)
                {
                    //only content has/needs this info
                    columns.Add("published.versionId as publishedVersion");
                    columns.Add("document.versionId as newestVersion");
                    columns.Add("contentversion.id as versionId");
                }
                
                columns.Add("contenttype.alias");
                columns.Add("contenttype.icon");
                columns.Add("contenttype.thumbnail");
                columns.Add("contenttype.isContainer");
            }

            //Creates an SQL query to return a single row for the entity

            var entitySql = new Sql()
                .Select(columns.ToArray())
                .From("umbracoNode umbracoNode");

            if (isContent || isMedia)
            {
                entitySql.InnerJoin("cmsContent content").On("content.nodeId = umbracoNode.id");

                if (isContent)
                {
                    //only content has/needs this info
                    entitySql                        
                        .InnerJoin("cmsDocument document").On("document.nodeId = umbracoNode.id")
                        .InnerJoin("cmsContentVersion contentversion").On("contentversion.VersionId = document.versionId")
                        .LeftJoin("(SELECT nodeId, versionId FROM cmsDocument WHERE published = 1) as published")
                        .On("umbracoNode.id = published.nodeId");
                }

                entitySql.LeftJoin("cmsContentType contenttype").On("contenttype.nodeId = content.contentType");
            }

            entitySql.LeftJoin("umbracoNode parent").On("parent.parentID = umbracoNode.id");

            if (customFilter != null)
            {
                customFilter(entitySql);
            }

            return entitySql;
        }

        protected virtual Sql GetBaseWhere(Func<bool, bool, Action<Sql>, Sql> baseQuery, bool isContent, bool isMedia, Action<Sql> filter, Guid nodeObjectType)
        {
            var sql = baseQuery(isContent, isMedia, filter)
                .Where("umbracoNode.nodeObjectType = @NodeObjectType", new { NodeObjectType = nodeObjectType });

            if (isContent)            {                sql.Where("document.newest = 1");            }

            return sql;
        }

        protected virtual Sql GetBaseWhere(Func<bool, bool, Action<Sql>, Sql> baseQuery, bool isContent, bool isMedia, int id)
        {
            var sql = baseQuery(isContent, isMedia, null)
                .Where("umbracoNode.id = @Id", new { Id = id });

            if (isContent)            {                sql.Where("document.newest = 1");            }

            sql.Append(GetGroupBy(isContent, isMedia));

            return sql;
        }

        protected virtual Sql GetBaseWhere(Func<bool, bool, Action<Sql>, Sql> baseQuery, bool isContent, bool isMedia, Guid key)
        {
            var sql = baseQuery(isContent, isMedia, null)
                .Where("umbracoNode.uniqueID = @UniqueID", new {UniqueID = key});

            if (isContent)            {                sql.Where("document.newest = 1");            }

            sql.Append(GetGroupBy(isContent, isMedia));

            return sql;
        }

        protected virtual Sql GetBaseWhere(Func<bool, bool, Action<Sql>, Sql> baseQuery, bool isContent, bool isMedia, Guid nodeObjectType, int id)
        {
            var sql = baseQuery(isContent, isMedia, null)
                .Where("umbracoNode.id = @Id AND umbracoNode.nodeObjectType = @NodeObjectType",
                       new {Id = id, NodeObjectType = nodeObjectType});

            if (isContent)            {                sql.Where("document.newest = 1");            }

            return sql;
        }

        protected virtual Sql GetBaseWhere(Func<bool, bool, Action<Sql>, Sql> baseQuery, bool isContent, bool isMedia, Guid nodeObjectType, Guid key)
        {
            var sql = baseQuery(isContent, isMedia, null)
                .Where("umbracoNode.uniqueID = @UniqueID AND umbracoNode.nodeObjectType = @NodeObjectType",
                       new { UniqueID = key, NodeObjectType = nodeObjectType });

            if (isContent)            {                sql.Where("document.newest = 1");            }

            return sql;
        }

        protected virtual Sql GetGroupBy(bool isContent, bool isMedia, bool includeSort = true)        {            var columns = new List<object>            {                "umbracoNode.id",                "umbracoNode.trashed",                "umbracoNode.parentID",                "umbracoNode.nodeUser",                "umbracoNode.level",                "umbracoNode.path",                "umbracoNode.sortOrder",                "umbracoNode.uniqueID",                "umbracoNode.text",                "umbracoNode.nodeObjectType",                "umbracoNode.createDate"            };
            if (isContent || isMedia)            {                if (isContent)                {                    columns.Add("published.versionId");                    columns.Add("document.versionId");                    columns.Add("contentversion.id");                }                columns.Add("contenttype.alias");                columns.Add("contenttype.icon");                columns.Add("contenttype.thumbnail");                columns.Add("contenttype.isContainer");                            }

            var sql = new Sql()                .GroupBy(columns.ToArray());
            if (includeSort)            {                sql = sql.OrderBy("umbracoNode.sortOrder");            }
            return sql;        }

        #endregion

        /// <summary>
        /// Dispose disposable properties
        /// </summary>
        /// <remarks>
        /// Ensure the unit of work is disposed
        /// </remarks>
        protected override void DisposeResources()
        {
            UnitOfWork.DisposeIfDisposable();
        }

        #region private classes
        
        [ExplicitColumns]
        internal class UmbracoPropertyDto
        {
            [Column("propertyEditorAlias")]
            public string PropertyEditorAlias { get; set; }

            [Column("propertyTypeAlias")]
            public string PropertyAlias { get; set; }

            [Column("dataNvarchar")]
            public string NVarcharValue { get; set; }

            [Column("dataNtext")]
            public string NTextValue { get; set; }
        }

        /// <summary>
        /// This is a special relator in that it is not returning a DTO but a real resolved entity and that it accepts
        /// a dynamic instance.
        /// </summary>
        /// <remarks>
        /// We're doing this because when we query the db, we want to use dynamic so that it returns all available fields not just the ones
        ///     defined on the entity so we can them to additional data
        /// </remarks>
        internal class UmbracoEntityRelator
        {
            internal UmbracoEntity Current;
            private readonly UmbracoEntityFactory _factory = new UmbracoEntityFactory();

            internal UmbracoEntity Map(dynamic a, UmbracoPropertyDto p)
            {
                // Terminating call.  Since we can return null from this function
                // we need to be ready for PetaPoco to callback later with null
                // parameters
                if (a == null)
                    return Current;

                // Is this the same UmbracoEntity as the current one we're processing
                if (Current != null && Current.Key == a.uniqueID)
                {
                    if (p != null && p.PropertyAlias.IsNullOrWhiteSpace() == false)
                    {
                        // Add this UmbracoProperty to the current additional data
                        Current.AdditionalData[p.PropertyAlias] = new UmbracoEntity.EntityProperty
                        {
                            PropertyEditorAlias = p.PropertyEditorAlias,
                            Value = p.NTextValue.IsNullOrWhiteSpace()
                                ? p.NVarcharValue
                                : p.NTextValue.ConvertToJsonIfPossible()
                        };    
                    }

                    // Return null to indicate we're not done with this UmbracoEntity yet
                    return null;
                }

                // This is a different UmbracoEntity to the current one, or this is the 
                // first time through and we don't have a Tab yet

                // Save the current UmbracoEntityDto
                var prev = Current;

                // Setup the new current UmbracoEntity
                
                Current = _factory.BuildEntityFromDynamic(a);

                if (p != null && p.PropertyAlias.IsNullOrWhiteSpace() == false)
                {
                    //add the property/create the prop list if null
                    Current.AdditionalData[p.PropertyAlias] = new UmbracoEntity.EntityProperty
                    {
                        PropertyEditorAlias = p.PropertyEditorAlias,
                        Value = p.NTextValue.IsNullOrWhiteSpace()
                            ? p.NVarcharValue
                            : p.NTextValue.ConvertToJsonIfPossible()
                    };
                }

                // Return the now populated previous UmbracoEntity (or null if first time through)
                return prev;
            }
        }

        private class EntityDefinitionCollection : KeyedCollection<int, EntityDefinition>
        {
            protected override int GetKeyForItem(EntityDefinition item)
            {
                return item.Id;
            }

            /// <summary>
            /// if this key already exists if it does then we need to check
            /// if the existing item is 'older' than the new item and if that is the case we'll replace the older one
            /// </summary>
            /// <param name="item"></param>
            /// <returns></returns>
            public bool AddOrUpdate(EntityDefinition item)
            {
                if (Dictionary == null)
                {
                    base.Add(item);
                    return true;
                }

                var key = GetKeyForItem(item);
                EntityDefinition found;
                if (TryGetValue(key, out found))
                {
                    //it already exists and it's older so we need to replace it
                    if (item.VersionId > found.VersionId)
                    {
                        var currIndex = Items.IndexOf(found);
                        if (currIndex == -1)
                            throw new IndexOutOfRangeException("Could not find the item in the list: " + found.Id);

                        //replace the current one with the newer one
                        SetItem(currIndex, item);
                        return true;
                    }
                    //could not add or update
                    return false;
                }

                base.Add(item);
                return true;
            }

            private bool TryGetValue(int key, out EntityDefinition val)
            {
                if (Dictionary == null)
                {
                    val = null;
                    return false;
                }
                return Dictionary.TryGetValue(key, out val);
            }
        }

        private class EntityDefinition
        {
            private readonly UmbracoEntityFactory _factory;
            private readonly dynamic _entity;
            private readonly bool _isContent;
            private readonly bool _isMedia;

            public EntityDefinition(UmbracoEntityFactory factory, dynamic entity, bool isContent, bool isMedia)
            {
                _factory = factory;
                _entity = entity;
                _isContent = isContent;
                _isMedia = isMedia;
            }

            public IUmbracoEntity BuildFromDynamic()
            {
                return _factory.BuildEntityFromDynamic(_entity);
            }

            public int Id
            {
                get { return _entity.id; }
            }

            public int VersionId
            {
                get
                {
                    if (_isContent || _isMedia)
                    {
                        return _entity.versionId;
                    }
                    return _entity.id;
                }
            }
        }
        #endregion
    }
}