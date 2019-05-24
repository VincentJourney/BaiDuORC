using BaiDuOCR.Model.Entity;
using Core.FrameWork;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BaiDuOCR.Core
{
    public class OCRDAL
    {
        /// <summary>
        /// Model
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="whereStr"></param>
        /// <returns></returns>
        public T GetModel<T>(string whereStr = "") =>
             DbContext.Query<T>($@"SELECT * FROM {typeof(T).Name} where 1=1  {whereStr}").ToList().FirstOrDefault();

        /// <summary>
        /// List
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="whereStr"></param>
        /// <returns></returns>
        public List<T> GetList<T>(string whereStr = "") =>
            DbContext.Query<T>($@"SELECT * FROM {typeof(T).Name} where 1=1 {whereStr}").ToList();

    }
}
