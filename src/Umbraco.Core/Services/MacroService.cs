using System;
using System.Collections.Generic;
using System.Linq;
using Umbraco.Core.Auditing;
using Umbraco.Core.Events;
using Umbraco.Core.Models;
using Umbraco.Core.Persistence;
using Umbraco.Core.Persistence.Querying;
using Umbraco.Core.Persistence.UnitOfWork;

namespace Umbraco.Core.Services
{
    /// <summary>
    /// Represents the Macro Service, which is an easy access to operations involving <see cref="IMacro"/>
    /// </summary>
    internal class MacroService : IMacroService
    {
	    private readonly RepositoryFactory _repositoryFactory;
        private readonly IDatabaseUnitOfWorkProvider _uowProvider;

        public MacroService()
            : this(new PetaPocoUnitOfWorkProvider(), new RepositoryFactory())
        {
        }
        
        public MacroService(IDatabaseUnitOfWorkProvider provider)
            : this(provider, new RepositoryFactory())
        {
        }

        public MacroService(IDatabaseUnitOfWorkProvider provider, RepositoryFactory repositoryFactory)
        {
            _uowProvider = provider;
            _repositoryFactory = repositoryFactory;
        }


        /// <summary>
        /// Gets an <see cref="IMacro"/> object by its alias
        /// </summary>
        /// <param name="alias">Alias to retrieve an <see cref="IMacro"/> for</param>
        /// <returns>An <see cref="IMacro"/> object</returns>
        public IMacro GetByAlias(string alias)
        {
            using (var repository = _repositoryFactory.CreateMacroRepository(_uowProvider.GetUnitOfWork()))
            {
                var q = new Query<IMacro>();
                q.Where(macro => macro.Alias == alias);
                return repository.GetByQuery(q).FirstOrDefault();
            }
        }

        /// <summary>
        /// Gets a list all available <see cref="IMacro"/> objects
        /// </summary>
        /// <param name="aliases">Optional array of aliases to limit the results</param>
        /// <returns>An enumerable list of <see cref="IMacro"/> objects</returns>
        public IEnumerable<IMacro> GetAll(params string[] aliases)
        {
            using (var repository = _repositoryFactory.CreateMacroRepository(_uowProvider.GetUnitOfWork()))
            {
                if (aliases.Any())
                {
                    var q = new Query<IMacro>();
                    foreach (var alias in aliases)
                    {
                        q.Where(macro => macro.Alias == alias);
                    }

                    return repository.GetByQuery(q);
                }

                return repository.GetAll();
            }
        }

        /// <summary>
        /// Deletes an <see cref="IMacro"/>
        /// </summary>
        /// <param name="macro"><see cref="IMacro"/> to delete</param>
        /// <param name="userId">Optional id of the user deleting the macro</param>
        public void Delete(IMacro macro, int userId = 0)
        {
			if (Deleting.IsRaisedEventCancelled(new DeleteEventArgs<IMacro>(macro), this))
				return;

			var uow = _uowProvider.GetUnitOfWork();
			using (var repository = _repositoryFactory.CreateMacroRepository(uow))
			{
				repository.Delete(macro);
				uow.Commit();

				Deleted.RaiseEvent(new DeleteEventArgs<IMacro>(macro, false), this);
			}

			Audit.Add(AuditTypes.Delete, "Delete Macro performed by user", userId, -1);
        }

        /// <summary>
        /// Saves an <see cref="IMacro"/>
        /// </summary>
        /// <param name="macro"><see cref="IMacro"/> to save</param>
        /// <param name="userId">Optional Id of the user deleting the macro</param>
        public void Save(IMacro macro, int userId = 0)
        {
	        if (Saving.IsRaisedEventCancelled(new SaveEventArgs<IMacro>(macro), this)) 
				return;
	        
			var uow = _uowProvider.GetUnitOfWork();
	        using (var repository = _repositoryFactory.CreateMacroRepository(uow))
	        {
		        repository.AddOrUpdate(macro);
		        uow.Commit();

		        Saved.RaiseEvent(new SaveEventArgs<IMacro>(macro, false), this);
	        }

	        Audit.Add(AuditTypes.Save, "Save Macro performed by user", userId, -1);
        }

        ///// <summary>
        ///// Gets a list all available <see cref="IMacroPropertyType"/> plugins
        ///// </summary>
        ///// <returns>An enumerable list of <see cref="IMacroPropertyType"/> objects</returns>
        //public IEnumerable<IMacroPropertyType> GetMacroPropertyTypes()
        //{
        //    return MacroPropertyTypeResolver.Current.MacroPropertyTypes;
        //}

        ///// <summary>
        ///// Gets an <see cref="IMacroPropertyType"/> by its alias
        ///// </summary>
        ///// <param name="alias">Alias to retrieve an <see cref="IMacroPropertyType"/> for</param>
        ///// <returns>An <see cref="IMacroPropertyType"/> object</returns>
        //public IMacroPropertyType GetMacroPropertyTypeByAlias(string alias)
        //{
        //    return MacroPropertyTypeResolver.Current.MacroPropertyTypes.FirstOrDefault(x => x.Alias == alias);
        //}

        #region Event Handlers
        /// <summary>
        /// Occurs before Delete
        /// </summary>
		public static event TypedEventHandler<IMacroService, DeleteEventArgs<IMacro>> Deleting;

        /// <summary>
        /// Occurs after Delete
        /// </summary>
		public static event TypedEventHandler<IMacroService, DeleteEventArgs<IMacro>> Deleted;

        /// <summary>
        /// Occurs before Save
        /// </summary>
		public static event TypedEventHandler<IMacroService, SaveEventArgs<IMacro>> Saving;

        /// <summary>
        /// Occurs after Save
        /// </summary>
		public static event TypedEventHandler<IMacroService, SaveEventArgs<IMacro>> Saved;
        #endregion
    }
}