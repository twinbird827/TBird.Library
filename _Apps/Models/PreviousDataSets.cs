using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TBird.Core;
using TBird.DB.SQLite;
using Tensorflow.Keras.Layers;

namespace Netkeiba.Models
{
	public class PreviousDataSets
	{
		private Dictionary<string, List<RaceDetail>> _Horses = new();
		private Dictionary<string, List<RaceDetail>> _Jockeys = new();
		private Dictionary<string, List<RaceDetail>> _Trainers = new();
		private Dictionary<string, List<RaceDetail>> _Sires = new();
		private Dictionary<string, List<RaceDetail>> _DamSires = new();
		private Dictionary<string, List<RaceDetail>> _SireDamSires = new();
		private Dictionary<string, List<RaceDetail>> _Breeders = new();
		private Dictionary<string, List<RaceDetail>> _JockeyTrainers = new();
		private Dictionary<string, List<RaceDetail>> _TrackDistances = new();

		public List<RaceDetail> GetHorses(RaceDetail x) => _Horses.Get(x.Horse, new List<RaceDetail>());

		public List<RaceDetail> GetJockeys(RaceDetail x) => _Jockeys.Get(x.Jockey, new List<RaceDetail>());

		public List<RaceDetail> GetTrainers(RaceDetail x) => _Trainers.Get(x.Trainer, new List<RaceDetail>());

		public List<RaceDetail> GetSires(RaceDetail x) => _Sires.Get(x.Sire, new List<RaceDetail>());

		public List<RaceDetail> GetDamSires(RaceDetail x) => _DamSires.Get(x.DamSire, new List<RaceDetail>());

		public List<RaceDetail> GetSireDamSires(RaceDetail x) => _SireDamSires.Get(x.SireDamSire, new List<RaceDetail>());

		public List<RaceDetail> GetBreeders(RaceDetail x) => _Breeders.Get(x.Breeder, new List<RaceDetail>());

		public List<RaceDetail> GetJockeyTrainers(RaceDetail x) => _JockeyTrainers.Get(x.JockeyTrainer, new List<RaceDetail>());

		public List<RaceDetail> GetTrackDistances(RaceDetail x) => _TrackDistances.Get(x.Race.TrackDistance, new List<RaceDetail>());

		public void AddHistory(RaceDetail x)
		{
			void AddHistory(Dictionary<string, List<RaceDetail>> dic, RaceDetail tgt, string key)
			{
				if (!dic.ContainsKey(key))
				{
					dic.Add(key, new List<RaceDetail>());
				}
				dic[key].Insert(0, tgt);
				if (dic[key].Count > 1000)
				{
					dic[key].RemoveAt(1000 - 1);
				}
			}

			AddHistory(_Horses, x, x.Horse);
			AddHistory(_Jockeys, x, x.Jockey);
			AddHistory(_Trainers, x, x.Trainer);
			AddHistory(_Sires, x, x.Sire);
			AddHistory(_DamSires, x, x.DamSire);
			AddHistory(_SireDamSires, x, x.SireDamSire);
			AddHistory(_Breeders, x, x.Breeder);
			AddHistory(_JockeyTrainers, x, x.JockeyTrainer);
			AddHistory(_TrackDistances, x, x.Race.TrackDistance);
		}

		public async Task AddConnection(SQLiteControl conn, RaceDetail x)
		{
			async Task SetConnection(Dictionary<string, List<RaceDetail>> dic, string key, params (string Key, string Value)[] kvp)
			{
				if (!dic.ContainsKey(key)) dic[key] = await conn.GetShutsubaRaceDetailAsync(x.Race.RaceDate, kvp);
			}

			var tasks = new[]
			{
				SetConnection(_Horses, x.Horse, ("d.馬ID", x.Horse)),
				SetConnection(_Jockeys, x.Jockey, ("d.騎手ID", x.Jockey)),
				SetConnection(_Trainers, x.Trainer, ("d.調教師ID", x.Trainer)),
				SetConnection(_Breeders, x.Breeder, ("u.生産者ID", x.Breeder)),
				SetConnection(_Sires, x.Sire, ("u.父ID", x.Sire)),
				SetConnection(_DamSires, x.DamSire, ("u.母父ID", x.DamSire)),
				SetConnection(_SireDamSires, x.SireDamSire, ("u.父ID", x.Sire), ("u.母父ID", x.DamSire)),
				SetConnection(_JockeyTrainers, x.JockeyTrainer, ("d.騎手ID", x.Jockey), ("d.調教師ID", x.Trainer)),
				SetConnection(_TrackDistances, x.JockeyTrainer, ("h.馬場", x.Race.Track), ("h.距離", x.Race.Distance.Str())),
			};

			await tasks.WhenAll();
		}

		public void Clear()
		{
			_Horses.Clear();
			_Jockeys.Clear();
			_Trainers.Clear();
			_Sires.Clear();
			_DamSires.Clear();
			_SireDamSires.Clear();
			_Breeders.Clear();
			_JockeyTrainers.Clear();
		}
	}
}