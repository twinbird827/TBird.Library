using MathNet.Numerics.Statistics;
using Microsoft.ML.AutoML;
using Microsoft.ML.Data;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using TBird.Core;
using TBird.DB;
using TBird.DB.SQLite;
using TBird.Wpf;
using TBird.Wpf.Collections;
using TBird.Wpf.Controls;

namespace Netkeiba
{
	public class ModelViewModel : DialogViewModel
	{
		public ModelViewModel()
		{

		}

		public string NetkeibaId
		{
			get => _NetkeibaId;
			set => SetProperty(ref _NetkeibaId, value);
		}
		private string _NetkeibaId = AppSetting.Instance.NetkeibaId;

		public string NetkeibaPassword
		{
			get => _NetkeibaPassword;
			set => SetProperty(ref _NetkeibaPassword, value);
		}
		private string _NetkeibaPassword = AppSetting.Instance.NetkeibaPassword;

		//public IRelayCommand ClickMerge { get; }

		//public IRelayCommand ClickSave { get; }

		//public IRelayCommand ClickCorrelation { get; }

		//public IRelayCommand ClickOutput { get; }

		//public IRelayCommand ClickDeleteAll { get; }
	}
}