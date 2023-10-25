using Moviewer.Core.Controls;
using Moviewer.Nico.Core;
using System;
using System.Threading.Tasks;
using System.Windows.Input;
using TBird.Core;
using TBird.Wpf;

namespace Moviewer.Nico.Controls
{
	public class NicoSearchHistoryViewModel : ControlViewModel
	{
		public NicoSearchHistoryViewModel(INicoSearchHistoryParentViewModel parent, NicoSearchHistoryModel m) : base(null)
		{
			Parent = parent;

			m.AddOnPropertyChanged(this, (sender, e) =>
			{
				Word = m.Word;
			}, nameof(Word), true);

			m.AddOnPropertyChanged(this, (sender, e) =>
			{
				Type = m.Type;
			}, nameof(Type), true);

			m.AddOnPropertyChanged(this, (sender, e) =>
			{
				Date = m.Date;
			}, nameof(Date), true);

			Loaded.Add(async () =>
			{
				Display = await GetDisplay();
			});
			Loaded.Add(() =>
			{
				if (Display is UserViewModel vm)
				{
					vm.SetThumbnail();
				}
			});

			AddDisposed((sender, e) =>
			{
				Loaded.Dispose();
				OnDelete.TryDispose();
				OnDoubleClick.TryDispose();
				OnFavoriteAdd.TryDispose();
				OnFavoriteDel.TryDispose();
				OnKeyDown.TryDispose();

				Display.TryDispose();
			});
		}

		public INicoSearchHistoryParentViewModel Parent { get; private set; }

		public string Word
		{
			get => _Word;
			set => SetProperty(ref _Word, value);
		}
		private string _Word;

		public NicoSearchType Type
		{
			get => _Type;
			set => SetProperty(ref _Type, value);
		}
		private NicoSearchType _Type;

		public DateTime Date
		{
			get => _Date;
			set => SetProperty(ref _Date, value);
		}
		private DateTime _Date;

		public object Display
		{
			get => _Display;
			set => SetProperty(ref _Display, value);
		}
		private object _Display;

		private async Task<object> GetDisplay()
		{
			switch (Type)
			{
				case NicoSearchType.User:
					return new NicoUserViewModel().SetUserInfo(await NicoUserModel.GetUserInfo(Word));
				case NicoSearchType.Mylist:
					return new NicoMylistViewModel(new NicoMylistModel(Word, await NicoMylistModel.GetNicoMylistXml(Word)));
				default:
					return Word;
			}
		}

		public ICommand OnDelete => _OnDelete = _OnDelete ?? RelayCommand.Create(_ =>
		{
			Parent.NicoSearchHistoryOnDelete(this);
		});
		private ICommand _OnDelete;

		public ICommand OnDoubleClick => _OnDoubleClick = _OnDoubleClick ?? RelayCommand.Create(_ =>
		{
			Parent.NicoSearchHistoryOnDoubleClick(this);
		});
		private ICommand _OnDoubleClick;

		public ICommand OnKeyDown => _OnKeyDown = _OnKeyDown ?? RelayCommand.Create<KeyEventArgs>(e =>
		{
			if (e.Key == Key.Enter)
			{
				OnDoubleClick.Execute(null);
			}
		});
		private ICommand _OnKeyDown;

		public ICommand OnFavoriteAdd => _OnFavoriteAdd = _OnFavoriteAdd ?? RelayCommand.Create(_ =>
		{
			NicoModel.AddFavorite(Word, Type);
		});
		private ICommand _OnFavoriteAdd;

		public ICommand OnFavoriteDel => _OnFavoriteDel = _OnFavoriteDel ?? RelayCommand.Create(_ =>
		{
			NicoModel.DelFavorite(Word, Type);
		});
		private ICommand _OnFavoriteDel;

	}
}