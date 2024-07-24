using System;
using System.Linq;
using shmtu.terminal.desktop.Database.Source.UserData;
using shmtu.terminal.desktop.Models.User;
using SqlSugar;

namespace shmtu.terminal.desktop.Database.Manage.User;

public static class UserConfigureDb
{
    private static SqlSugarClient GetDbSource()
    {
        return new UserConfigureDbSource().GetNewDbObj();
    }

    public static void Init()
    {
        CreateDbIfNotExist();

        LoadUserConfigure();
    }

    private static void CreateDbIfNotExist()
    {
        var db = GetDbSource();

        if (!db.DbMaintenance.IsAnyTable(typeof(UserConfigure).FullName))
        {
            db.CodeFirst.InitTables(typeof(UserConfigure));
            Console.WriteLine("Create UserConfigure Table");
        }
    }

    public static void LoadUserConfigure()
    {
        var db = GetDbSource();

        var userConfigureList = db.Queryable<UserConfigure>().ToList();
        if (userConfigureList == null) return;

        UserConfigure.UserConfigureList.Clear();
        UserConfigure.UserConfigureList.AddRange(userConfigureList);
    }

    public static void SaveUserConfigure()
    {
        var db = GetDbSource();

        // 开始事务
        db.Ado.BeginTran();

        try
        {
            // 插入或更新UserConfigure信息
            foreach (var student in UserConfigure.UserConfigureList)
            {
                var existStudent = db.Queryable<UserConfigure>().Where(s => s.Name == student.Name).First();
                if (existStudent != null)
                {
                    // 更新操作
                    db.Updateable(student).ExecuteCommand();
                }
                else
                {
                    // 插入操作
                    db.Insertable(student).ExecuteCommand();
                }
            }

            // 获取数据库中所有Name，并与当前列表中的Name比较，删除多余的记录
            var dbNames = db.Queryable<UserConfigure>().Select(s => s.Name).ToList();
            if (dbNames != null)
            {
                var namesToRemove = dbNames.Except(
                    UserConfigure.UserConfigureList.Select(s => s.Name)
                ).ToList();
                if (namesToRemove.Any())
                {
                    db.Deleteable<UserConfigure>().In(namesToRemove).ExecuteCommand();
                }
            }

            // 提交事务
            db.Ado.CommitTran();
        }
        catch (Exception ex)
        {
            // 回滚事务
            db.Ado.RollbackTran();
            Console.WriteLine("操作失败：" + ex.Message);
        }
    }
}