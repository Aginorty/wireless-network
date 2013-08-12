﻿using System;
using System.Linq;
using System.Linq.Expressions;

namespace DataLayer.Repositories
{
    public interface IRepository<T> where T : class
    {
        void Insert(T entity);
        void Delete(T entity);
        IQueryable<T> SearchFor(Expression<Func<T, bool>> predicate);
        IQueryable<T> GetAll();
        T GetById(int id);
    }
}
