﻿using EvilMask.Emuera;
using MinorShift.Emuera.Sub;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using trerror = EvilMask.Emuera.Lang.Error;

namespace MinorShift.Emuera.Content;

static class AppContents
{
	static AppContents()
	{
		gList = [];
	}
	static Dictionary<string, AContentFile> resourceDic = [];
	static Dictionary<string, ASprite> imageDictionary = [];
	static Dictionary<int, GraphicsImage> gList;
	static Dictionary<string, ASprite> resourceImageDictionary = new Dictionary<string, ASprite>();

	// the ConstImages that has been loaded into memory. will free them in every SetBegin(BeginType.SHOP)
	public static HashSet<ConstImage> tempLoadedConstImages = new HashSet<ConstImage>();
	public static HashSet<GraphicsImage> tempLoadedGraphicsImages = new HashSet<GraphicsImage>();


	//static public T GetContent<T>(string name)where T :AContentItem
	//{
	//	if (name == null)
	//		return null;
	//	name = name.ToUpper();
	//	if (!itemDic.ContainsKey(name))
	//		return null;
	//	return itemDic[name] as T;
	//}
	static public GraphicsImage GetGraphics(int i)
	{
		if (gList.ContainsKey(i))
			return gList[i];
		GraphicsImage g = new(i);
		gList[i] = g;
		return g;
	}

	static public ASprite GetSprite(string name)
	{
		if (name == null)
			return null;
		name = name.ToUpper();
		if (!imageDictionary.ContainsKey(name))
			return null;
		return imageDictionary[name];
	}

	static public void SpriteDispose(string name)
	{
		if (name == null)
			return;
		name = name.ToUpper();
		if (!imageDictionary.ContainsKey(name))
			return;
		imageDictionary[name].Dispose();
		imageDictionary.Remove(name);
	}

	static public long SpriteDisposeAll(bool delCsvImage)
	{
		int sprites = imageDictionary.Count;
		int csprites = resourceImageDictionary.Count;
		if (delCsvImage)
		{
			imageDictionary.Clear();
			resourceImageDictionary.Clear();
			return sprites;
		}
		else
		{
			imageDictionary = new Dictionary<string, ASprite>(resourceImageDictionary);
			return sprites - csprites;
		}
	}

	static public void CreateSpriteG(string imgName, GraphicsImage parent, Rectangle rect)
	{
		if (string.IsNullOrEmpty(imgName))
			throw new ArgumentOutOfRangeException();
		imgName = imgName.ToUpper();
		SpriteG newCImg = new(imgName, parent, rect);
		imageDictionary[imgName] = newCImg;
	}

	internal static void CreateSpriteAnime(string imgName, int w, int h)
	{
		if (string.IsNullOrEmpty(imgName))
			throw new ArgumentOutOfRangeException();
		imgName = imgName.ToUpper();
		SpriteAnime newCImg = new SpriteAnime(imgName, new Size(w, h));
		imageDictionary[imgName] = newCImg;
	}

	static public bool LoadContents(bool reload)
	{
		if (!Directory.Exists(Program.ContentDir))
			return true;
		try
		{
			//resourcesフォルダ内の全てのcsvファイルを探索する
			string[] csvFiles = Directory.GetFiles(Program.ContentDir, "*.csv", SearchOption.AllDirectories);
			foreach (var filepath in csvFiles)
			{
				//".csv"のみを拾うように
				if (0 != ".csv".CompareTo(Path.GetExtension(filepath).ToLower()))
					continue;
				//アニメスプライト宣言。nullでないとき、フレーム追加モード
				SpriteAnime currentAnime = null;
				string directory = Path.GetDirectoryName(filepath).ToUpper() + "\\";
				string filename = Path.GetFileName(filepath);
				string[] lines = File.ReadAllLines(filepath, EncodingHandler.DetectEncoding(filepath));
				int lineNo = 0;
				if (reload)
				{
					foreach (string key in resourceImageDictionary.Keys)
					{
						imageDictionary.Remove(key);
					}
					resourceImageDictionary.Clear();
					foreach (var img in resourceDic.Values)
						img.Dispose();
					resourceDic.Clear();
				}
				foreach (var line in lines)
				{
					lineNo++;
					if (line.Length == 0)
						continue;
					string str = line.Trim();
					if (str.Length == 0 || str.StartsWith(";"))
						continue;
					string[] tokens = str.Split(',');
					//AContentItem item = CreateFromCsv(tokens);
					ScriptPosition sp = new(filename, lineNo);

					if (CreateFromCsv(tokens, directory, currentAnime, sp) is ASprite item)
					{
						//アニメスプライト宣言ならcurrentAnime上書きしてフレーム追加モードにする。そうでないならnull
						currentAnime = item as SpriteAnime;
						if (reload && resourceImageDictionary.ContainsKey(item.Name))
							resourceImageDictionary.Remove(item.Name);
						if (!resourceImageDictionary.ContainsKey(item.Name))
						{
							resourceImageDictionary.Add(item.Name, item);
						}
						else
						{
							ParserMediator.Warn(string.Format(trerror.SpriteNameAlreadyUsed.Text, item.Name), sp, 0);
							item.Dispose();
						}
					}
				}
			}
		}
		catch
		{
			return false;
			//throw new CodeEE("リソースファイルのロード中にエラーが発生しました");
		}
		imageDictionary = new Dictionary<string, ASprite>(resourceImageDictionary);
		return true;
	}

	static public void UnloadContents()
	{
		foreach (var img in resourceDic.Values)
			img.Dispose();
		resourceDic.Clear();
		imageDictionary.Clear();
		resourceImageDictionary.Clear();
		foreach (var graph in gList.Values)
			graph.GDispose();
		gList.Clear();
	}

	//タイトルに戻る時用（コードの変更はないので、動的に作られた分だけ削除）
	static public void UnloadGraphicList()
	{
		foreach (var graph in gList.Values)
			graph.GDispose();
		gList.Clear();
	}
	// used for clean ConstImage from memory
	static public void UnloadTempLoadedConstImageNames()
	{
		foreach (ConstImage img in tempLoadedConstImages)
			img.Dispose();
		tempLoadedConstImages.Clear();
	}
	// used for clean GraphicsImage from memory
	static public void UnloadTempLoadedGraphicsImageNames()
	{
		foreach (GraphicsImage img in tempLoadedGraphicsImages)
			if (img.useImgList)
				img.UnLoad();
		tempLoadedGraphicsImages.Clear();
	}
	/// <summary>
	/// resourcesフォルダ中のcsvの1行を読んで新しいリソースを作る(or既存のアニメーションスプライトに1フレーム追加する)
	/// </summary>
	/// <param name="tokens"></param>
	/// <param name="dir"></param>
	/// <param name="currentAnime"></param>
	/// <param name="sp"></param>
	/// <returns></returns>
	static private AContentItem CreateFromCsv(string[] tokens, string dir, SpriteAnime currentAnime, ScriptPosition sp)
	{
		if (tokens.Length < 2)
			return null;
		string name = tokens[0].Trim().ToUpper();//
		string arg2 = tokens[1].ToUpper();//画像ファイル名
		if (name.Length == 0 || arg2.Length == 0)
			return null;
		//アニメーションスプライト宣言
		if (arg2 == "ANIME")
		{
			if (tokens.Length < 4)
			{
				ParserMediator.Warn(trerror.NotDeclaredAnimationSpriteSize.Text, sp, 1);
				return null;
			}
			//w,h
			int[] sizeValue = new int[2];
			bool sccs = true;
			for (int i = 0; i < 2; i++)
				sccs &= int.TryParse(tokens[i + 2], out sizeValue[i]);
			if (!sccs || sizeValue[0] <= 0 || sizeValue[1] <= 0 || sizeValue[0] > AbstractImage.MAX_IMAGESIZE || sizeValue[1] > AbstractImage.MAX_IMAGESIZE)
			{
				ParserMediator.Warn(trerror.InvalidAnimationSpriteSize.Text, sp, 1);
				return null;
			}
			SpriteAnime anime = new(name, new Size(sizeValue[0], sizeValue[1]));

			return anime;
		}
		//アニメ宣言以外（アニメ用フレーム含む

		if (arg2.IndexOf('.', StringComparison.Ordinal) < 0)
		{
			ParserMediator.Warn(string.Format(trerror.MissingSecondArgumentExtension.Text, arg2), sp, 1);
			return null;
		}
		string parentName = dir + arg2;


		//親画像のロードConstImage
		if (!resourceDic.ContainsKey(parentName))
		{
			string filepath = parentName;
			if (!File.Exists(filepath))
			{
				ParserMediator.Warn(string.Format(trerror.NotExistImageFile.Text, arg2), sp, 1);
				return null;
			}
			#region EM_私家版_webp
			// Bitmap bmp = new Bitmap(filepath);
			var bmp = Utils.LoadImage(filepath);
			#endregion
			if (bmp == null)
			{
				ParserMediator.Warn(string.Format(trerror.FailedLoadFile.Text, arg2), sp, 1);
				return null;
			}
			if (bmp.Width > AbstractImage.MAX_IMAGESIZE || bmp.Height > AbstractImage.MAX_IMAGESIZE)
			{
				//1824-2 すでに8192以上の幅を持つ画像を利用したバリアントが存在してしまっていたため、警告しつつ許容するように変更
				//	bmp.Dispose();
				ParserMediator.Warn(string.Format(trerror.TooLargeImageFile.Text, AbstractImage.MAX_IMAGESIZE.ToString(), arg2), sp, 1);
				//return null;
			}
			ConstImage img = new(parentName);
			img.CreateFrom(bmp, filepath, Config.TextDrawingMode == TextDrawingMode.WINAPI);
			if (!img.IsCreated)
			{
				ParserMediator.Warn(string.Format(trerror.FailedCreateResource.Text, arg2), sp, 1);
				return null;
			}
			resourceDic.Add(parentName, img);
			img.Dispose();
		}
		if (!(resourceDic[parentName] is ConstImage parentImage) || !parentImage.IsCreated)
		{
			ParserMediator.Warn(string.Format(trerror.SpriteCreateFromFailedResource.Text, arg2), sp, 1);
			return null;
		}
		Rectangle rect = new(new Point(0, 0), parentImage.Bitmap.Size);
		Size size = rect.Size;
		Point pos = new();
		int delay = 1000;
		//name,parentname, x,y,w,h ,offset_x,offset_y, delayTime, destX,destY
		if (tokens.Length >= 6)//x,y,w,h
		{
			int[] outValue = new int[4];
			bool sccs = true;
			for (int i = 0; i < 4; i++)
				sccs &= int.TryParse(tokens[i + 2], out outValue[i]);
			if (sccs)
			{
				rect = new Rectangle(outValue[0], outValue[1], outValue[2], outValue[3]);
				size = rect.Size;
				if (rect.Width <= 0 || rect.Height <= 0)
				{
					ParserMediator.Warn(string.Format(trerror.SpriteSizeIsNegatibe.Text, name), sp, 1);
					return null;
				}
				if (!rect.IntersectsWith(new Rectangle(0, 0, parentImage.Width, parentImage.Height)))
				{
					ParserMediator.Warn(string.Format(trerror.OoRParentImage.Text, name), sp, 1);
					return null;
				}
			}
			if (tokens.Length >= 8)
			{
				sccs = true;
				for (int i = 0; i < 2; i++)
					sccs &= int.TryParse(tokens[i + 6], out outValue[i]);
				if (sccs)
					pos = new Point(outValue[0], outValue[1]);
				if (tokens.Length >= 9)
				{
					sccs = int.TryParse(tokens[8], out delay);
					if (sccs && delay <= 0)
					{
						ParserMediator.Warn(string.Format(trerror.FrameTimeIsNegative.Text, name), sp, 1);
						return null;
					}
					if (tokens.Length >= 11)
					{
						sccs = true;
						for (int i = 0; i < 2; i++)
							sccs &= int.TryParse(tokens[i + 9], out outValue[i]);
						if (sccs)
							size = new Size(outValue[0], outValue[1]);
					}
				}
			}
		}
		//既存のスプライトに対するフレーム追加
		if (currentAnime != null && currentAnime.Name == name)
		{
			if (!currentAnime.AddFrame(parentImage, rect, pos, delay))
			{
				ParserMediator.Warn(string.Format(trerror.FailedAddSpriteFrame.Text, arg2), sp, 1);
				return null;
			}
			return null;
		}

		//新規スプライト定義
		ASprite image = new SpriteF(name, parentImage, rect, pos, size);
		return image;
	}
}
