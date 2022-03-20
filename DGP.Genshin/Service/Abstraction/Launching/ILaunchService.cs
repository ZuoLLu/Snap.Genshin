﻿using DGP.Genshin.DataModel.Launching;
using IniParser.Model;
using Snap.Data.Primitive;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace DGP.Genshin.Service.Abstraction.Launching
{
    /// <summary>
    /// 游戏启动服务
    /// </summary>
    public interface ILaunchService
    {
        /// <summary>
        /// 游戏配置文件
        /// </summary>
        IniData GameConfig { get; }

        /// <summary>
        /// 启动器配置文件
        /// </summary>
        IniData LauncherConfig { get; }
        WorkWatcher GameWatcher { get; }

        /// <summary>
        /// 异步启动游戏
        /// </summary>
        /// <param name="scheme">启动方案</param>
        /// <param name="failAction">启动失败回调</param>
        Task LaunchAsync(LaunchOption option, Action<Exception> failAction);

        /// <summary>
        /// 加载配置文件数据
        /// </summary>
        /// <param name="launcherPath">启动器路径</param>
        /// <returns>是否加载成功</returns>
        bool TryLoadIniData(string? launcherPath);

        /// <summary>
        /// 启动官方启动器
        /// </summary>
        /// <param name="failAction">启动失败回调</param>
        void OpenOfficialLauncher(Action<Exception>? failAction);

        /// <summary>
        /// 保存启动方案到配置文件
        /// </summary>
        /// <param name="scheme">启动方案</param>
        void SaveLaunchScheme(LaunchScheme? scheme);

        /// <summary>
        /// 选择启动器路径, 若提供有效的路径则直接返回, 否则应使用户选择路径
        /// </summary>
        /// <param name="launcherPath">待检验的启动器路径</param>
        /// <returns>启动器路径</returns>
        string? SelectLaunchDirectoryIfIncorrect(string? launcherPath);
        void SaveAllAccounts(IEnumerable<GenshinAccount> accounts);
        ObservableCollection<GenshinAccount> LoadAllAccount();
        GenshinAccount? GetFromRegistry();
        bool SetToRegistry(GenshinAccount? account);
        void SetTargetFPSDynamically(int targetFPS);
    }
}