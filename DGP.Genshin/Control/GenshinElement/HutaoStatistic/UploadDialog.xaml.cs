﻿using DGP.Genshin.HutaoAPI.PostModel;
using ModernWpf.Controls;

namespace DGP.Genshin.Control.GenshinElement.HutaoStatistic
{
    public partial class UploadDialog : ContentDialog
    {
        public UploadDialog(PlayerRecord playerRecord)
        {
            DataContext = playerRecord;
            InitializeComponent();
        }
    }
}