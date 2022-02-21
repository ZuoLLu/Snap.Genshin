﻿using DGP.Genshin.Control;
using DGP.Genshin.Control.Title;
using DGP.Genshin.Core.Plugins;
using DGP.Genshin.DataModel.WebViewLobby;
using DGP.Genshin.Helper;
using DGP.Genshin.Helper.Notification;
using DGP.Genshin.Message;
using DGP.Genshin.MiHoYoAPI.GameRole;
using DGP.Genshin.MiHoYoAPI.Sign;
using DGP.Genshin.Page;
using DGP.Genshin.Service.Abstraction;
using DGP.Genshin.ViewModel;
using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Toolkit.Mvvm.Messaging;
using Microsoft.Toolkit.Uwp.Notifications;
using ModernWpf.Controls.Primitives;
using Snap.Reflection;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace DGP.Genshin
{
    public partial class MainWindow : Window,
        IRecipient<SplashInitializationCompletedMessage>,
        IRecipient<NavigateRequestMessage>,
        IRecipient<BackgroundOpacityChangedMessage>
    {
        //make sure while post-initializing, main window can't be closed
        //prevent System.NullReferenceException
        //cause we have some async operation in initialization so we can't use lock
        private readonly SemaphoreSlim initializingWindow = new(1, 1);
        private bool hasInitializeCompleted = false;

        private static bool hasEverOpen = false;
        private static bool hasEverClose = false;

        private readonly INavigationService navigationService;
        private readonly BackgroundLoader backgroundLoader;

        /// <summary>
        /// do not set DataContext for mainwindow
        /// </summary>
        public MainWindow()
        {
            InitializeContent();
            //randomly load a image as background
            backgroundLoader = new(this);
            backgroundLoader.LoadWallpaper();
            //initialize NavigationService
            navigationService = App.AutoWired<INavigationService>();
            navigationService.NavigationView = NavView;
            navigationService.Frame = ContentFrame;
            //register messages
            App.Messenger.Register<SplashInitializationCompletedMessage>(this);
            App.Messenger.Register<NavigateRequestMessage>(this);
            App.Messenger.Register<BackgroundOpacityChangedMessage>(this);
        }

        private void InitializeContent()
        {
            InitializeComponent();
            ISettingService settingService = App.AutoWired<ISettingService>();
            //restore width and height from setting
            Width = Setting2.MainWindowWidth.Get();
            Height = Setting2.MainWindowHeight.Get();
            //restore pane state
            NavView.IsPaneOpen = Setting2.IsNavigationViewPaneOpen.Get();
        }

        ~MainWindow()
        {
            App.Messenger.Unregister<SplashInitializationCompletedMessage>(this);
            App.Messenger.Unregister<NavigateRequestMessage>(this);
            App.Messenger.Unregister<BackgroundOpacityChangedMessage>(this);
        }

        public async void Receive(SplashInitializationCompletedMessage viewModelReference)
        {
            initializingWindow.Wait();
            ISettingService settingService = App.AutoWired<ISettingService>();
            SplashViewModel splashViewModel = viewModelReference.Value;
            PrepareTitleBarArea();
            AddAdditionalWebViewNavigationViewItems();
            AddAdditionalPluginsNavigationViewItems();
            //preprocess
            if (!hasEverOpen)
            {
                DoUpdateFlowAsync();
                //签到
                if (Setting2.AutoDailySignInOnLaunch.Get())
                {
                    await SignInOnStartUp(splashViewModel);
                }
                //任务栏
                if (Setting2.IsTaskBarIconEnabled.Get())
                {
                    DoTaskbarFlow();
                }
            }
            splashViewModel.CurrentStateDescription = "完成";
            splashViewModel.IsSplashNotVisible = true;
            await Task.Delay(500);
            navigationService.Navigate<HomePage>(isSyncTabRequested: true);
            //before call Close() in this method,must release initializingWindow.
            initializingWindow.Release();
            hasInitializeCompleted = true;

            if (!hasEverOpen)
            {
                if (Setting2.IsTaskBarIconEnabled.Get() && (App.Current.NotifyIcon is not null))
                {
                    if (Setting2.CloseMainWindowAfterInitializaion.Get())
                    {
                        Close();
                    }
                }
            }
            //设置已经打开过状态
            hasEverOpen = true;
        }
        public void Receive(NavigateRequestMessage message)
        {
            navigationService.Navigate(message);
        }
        public void Receive(BackgroundOpacityChangedMessage message)
        {
            if (BackgroundGrid.Background is ImageBrush brush)
            {
                brush.Opacity = message.Value;
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            ISettingService settingService = App.AutoWired<ISettingService>();
            Setting2.IsNavigationViewPaneOpen.Set(NavView.IsPaneOpen);
            initializingWindow.Wait();
            base.OnClosing(e);
            initializingWindow.Release();

            bool isTaskbarIconEnabled = Setting2.IsTaskBarIconEnabled.Get() && (App.Current.NotifyIcon is not null);

            if (hasInitializeCompleted && isTaskbarIconEnabled)
            {
                if (!hasEverClose)
                {
                    SecureToastNotificationContext.TryCatch(() =>
                    new ToastContentBuilder()
                    .AddText("Snap Genshin 已转入后台运行\n点击托盘图标以显示主窗口")
                    .Show());
                    hasEverClose = true;
                }
            }
            else
            {
                App.Current.Shutdown();
            }
        }
        private void MainWindowSizeChanged(object sender, SizeChangedEventArgs e)
        {
            ISettingService settingService = App.AutoWired<ISettingService>();
            if (WindowState == WindowState.Normal)
            {
                settingService.Set(Setting2.MainWindowWidth, Width);
                settingService.Set(Setting2.MainWindowHeight, Height);
            }
        }

        private void DoTaskbarFlow()
        {
            App.Current.NotifyIcon ??= App.Current.FindResource("TaskbarIcon") as TaskbarIcon;
            App.Current.NotifyIcon!.DataContext = App.AutoWired<TaskbarIconViewModel>();
        }

        #region Aditional NavigationViewItems
        /// <summary>
        /// 添加从插件引入的额外的导航页签
        /// </summary>
        private void AddAdditionalPluginsNavigationViewItems()
        {
            foreach (IPlugin plugin in App.Current.PluginService.Plugins)
            {
                plugin.ForEachAttribute<ImportPageAttribute>(importPage => navigationService.AddToNavigation(importPage));
            }
        }
        /// <summary>
        /// 添加额外的网页导航页签
        /// </summary>
        private void AddAdditionalWebViewNavigationViewItems()
        {
            ObservableCollection<WebViewEntry>? entries = App.AutoWired<WebViewLobbyViewModel>().Entries;
            navigationService.AddWebViewEntries(entries);
        }
        #endregion

        /// <summary>
        /// 描述了自带的标题栏定义
        /// </summary>
        [ImportTitle(typeof(LaunchTitleBarButton), 200)]
        [ImportTitle(typeof(DailyNoteTitleBarButton), 150)]
        [ImportTitle(typeof(SignInTitleBarButton), 100)]
        [ImportTitle(typeof(JourneyLogTitleBarButton), 50)]
        [ImportTitle(typeof(UserInfoTitleBarButton), 0)]
        private class TitleDefinition { }

        /// <summary>
        /// 准备标题栏按钮
        /// </summary>
        /// <param name="splashView"></param>
        private void PrepareTitleBarArea()
        {
            List<ImportTitleAttribute> titleBarButtons = new();

            foreach (IPlugin plugin in App.Current.PluginService.Plugins)
            {
                plugin.ForEachAttribute<ImportTitleAttribute>(importTitle => titleBarButtons.Add(importTitle));
            }
            new TitleDefinition().ForEachAttribute<ImportTitleAttribute>(title => titleBarButtons.Add(title));

            IOrderedEnumerable<ImportTitleAttribute> filtered = titleBarButtons
                .Where(title => typeof(TitleBarButton).IsAssignableFrom(title.ButtonType))
                .OrderByDescending(title => title.Order);

            foreach (ImportTitleAttribute titleBarButton in filtered)
            {
                TitleBarStackPanel.Children.Add(Activator.CreateInstance(titleBarButton.ButtonType) as TitleBarButton);
            }
        }

        #region Sign In
        /// <summary>
        /// 对Cookie列表内的所有角色签到
        /// </summary>
        /// <param name="splashView"></param>
        /// <returns></returns>
        private async Task SignInOnStartUp(SplashViewModel splashView)
        {
            DateTime? latsSignInTime = Setting2.LastAutoSignInTime.Get();
            if (latsSignInTime < DateTime.Today)
            {
                splashView.CurrentStateDescription = "签到中...";
                await SignInAllAccountsRolesAsync();
            }
        }

        public static async Task SignInAllAccountsRolesAsync()
        {
            ICookieService cookieService = App.AutoWired<ICookieService>();
            ISettingService settingService = App.AutoWired<ISettingService>();

            cookieService.CookiesLock.EnterReadLock();
            foreach (string cookie in cookieService.Cookies)
            {
                List<UserGameRole> roles = await new UserGameRoleProvider(cookie).GetUserGameRolesAsync();
                foreach (UserGameRole role in roles)
                {
                    SignInResult? result = await new SignInProvider(cookie).SignInAsync(role);

                    settingService.Set(Setting2.LastAutoSignInTime, DateTime.Now);
                    bool isSignInSilently = Setting2.SignInSilently.Get();
                    SecureToastNotificationContext.TryCatch(() =>
                    new ToastContentBuilder()
                        .AddSignInHeader("米游社每日签到")
                        .AddText(role.ToString())
                        .AddText(result is null ? "签到失败" : "签到成功")
                        .Show(toast => { toast.SuppressPopup = isSignInSilently; }));
                }
            }
            cookieService.CookiesLock.ExitReadLock();
        }
        #endregion

        #region Update
        private async void DoUpdateFlowAsync()
        {
            await CheckUpdateAsync();
            ISettingService settingService = App.AutoWired<ISettingService>();
            IUpdateService updateService = App.AutoWired<IUpdateService>();
            Version? lastLaunchAppVersion = Setting2.AppVersion.Get();
            //first launch after update
            if (lastLaunchAppVersion < updateService.CurrentVersion)
            {
                settingService.Set(Setting2.AppVersion, updateService.CurrentVersion);
                //App.Current.Dispatcher.InvokeAsync
                new WhatsNewWindow { ReleaseNote = updateService.Release?.Body }.Show();
            }
        }
        private async Task CheckUpdateAsync()
        {
            UpdateState result = await App.AutoWired<IUpdateService>().CheckUpdateStateAsync();
            //force-update, debug code
            //result = UpdateState.NeedUpdate;
            switch (result)
            {
                case UpdateState.NeedUpdate:
                    {
                        SecureToastNotificationContext.TryCatch(() =>
                        new ToastContentBuilder()
                            .AddText("有新的更新可用")
                            .AddText(App.AutoWired<IUpdateService>().NewVersion?.ToString())
                            .AddButton(new ToastButton()
                                .SetContent("更新")
                                .AddArgument("action", "update")
                                .SetBackgroundActivation())
                            .AddButton(new ToastButtonDismiss("忽略"))
                            .Show());
                        break;
                    }
                case UpdateState.NotAvailable:
                    {
                        SecureToastNotificationContext.TryCatch(() =>
                        new ToastContentBuilder()
                            .AddText("检查更新失败")
                            .Show());
                        break;
                    }
                case UpdateState.IsNewestRelease:
                case UpdateState.IsInsiderVersion:
                default:
                    break;
            }
        }
        #endregion
    }


}
