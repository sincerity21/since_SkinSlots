using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Ace7LocalizationFormat.Formats;

namespace SkinSlotsCore;

public static class PawnCloneCheck
{
	public static void Run(string pawnRoot)
	{
		string templateSlot;
		string templateUasset = PawnLibrary.TemplatePath(pawnRoot, "f16c", "02", out templateSlot);
		if (templateSlot != "02")
		{
			throw new InvalidOperationException("Expected template slot 02, got " + templateSlot + ".");
		}
		string templateSlot2;
		string text = PawnLibrary.TemplatePath(pawnRoot, "f16c", "00", out templateSlot2);
		if (templateSlot2 != "03" || !text.EndsWith("AcePlayerPawn_f16c_s03.uasset", StringComparison.OrdinalIgnoreCase))
		{
			throw new InvalidOperationException("Skin 1 should clone the s03 pawn.");
		}
		string text2 = Path.Combine(Path.GetTempPath(), "SkinSlotsCheck_" + Guid.NewGuid().ToString("N"), "AcePlayerPawn_f16c_s12.uasset");
		try
		{
			PlaneSkins planeSkins = PawnLibrary.Describe(pawnRoot, "f16c");
			if (planeSkins.SkinCount != 12 || planeSkins.LastSuffix != 11 || planeSkins.NextSuffix != 12 || planeSkins.NextHangarSlot != 13)
			{
				throw new InvalidOperationException($"F-16C census was {planeSkins.SkinCount} skins, last s{planeSkins.LastSuffix:D2}, next slot {planeSkins.NextHangarSlot}.");
			}
			MountedSkin.PlaneTableInfo planeTableInfo = MountedSkin.ReadPlane(PawnLibrary.StockSkinTable(pawnRoot), "f16c");
			Require(planeTableInfo.MaxSkinNo == 11, "f16c max skin was " + planeTableInfo.MaxSkinNo);
			string text3 = MountedSkin.PlaneLabel(PawnLibrary.StockLocalizationDirectory(pawnRoot), "f16c");
			Require(text3 == "F-16C Fighting Falcon", "plane label was " + text3);
			string text4 = SkinSlotBuilder.PakFileName(text3, "Serdyukov", 13);
			Require(text4 == "SkinReSlotter_F-16C_Serdyukov_Skin13_P.pak", "pak name was " + text4);
			string text5 = Path.Combine(Path.GetTempPath(), "SkinSlotsMarker_" + Guid.NewGuid().ToString("N"));
			try
			{
				ModFolder.WriteMarker(text5);
				Require(ModFolder.MarkerIsValid(text5), "data pak marker was not written");
			}
			finally
			{
				if (Directory.Exists(text5))
				{
					Directory.Delete(text5, recursive: true);
				}
			}
			SkinSlotBuilder.StagePawn(templateUasset, text2, "f16c", "02", "12");
			byte[] item = AcePackage.ReadPlain(text2).Asset;
			string text6 = Encoding.ASCII.GetString(item);
			Require(text6.Contains("AcePlayerPawn_f16c_s12"), "cloned pawn was not renamed");
			Require(!text6.Contains("AcePlayerPawn_f16c_s02"), "template pawn name survived");
			Require(text6.Contains("f16c_12_Inst"), "body material was not retargeted");
			Require(text6.Contains("f16c_12_Decal_Inst"), "decal was left on the source slot");
			Require(text6.Contains("f16c_02_CP_Inst"), "cockpit reference was changed");
			Require(!text6.Contains("f16c_12_CP_Inst"), "cockpit was moved onto the new slot");
			Require(!text6.Contains("f16c_00_CP_Inst"), "cockpit was moved onto the base jet");
			Require(!text6.Contains("f16c_02_Inst"), "body was left on the source slot");
			Require(!text6.Contains("f16c_02_Decal"), "decal was left on the source slot");
			Require(text6.Contains("/f16c/12/"), "material folder was not retargeted");
			Require(text6.Contains("AcePlayerPawn_f16c_C"), "parent pawn reference was lost");
			Require(PlainHeader(text2), "cloned pawn was saved encrypted");
			string text7 = Path.Combine(Path.GetTempPath(), "SkinSlotsDecal_" + Guid.NewGuid().ToString("N"));
			SkinSlotBuilder.StageStockDecal(pawnRoot, text7, "f16c", "02", "12", null);
			byte[] item2 = AcePackage.ReadPlain(Path.Combine(text7, "Nimbus", "Content", "Vehicles", "Aircraft", "f16c", "12", "f16c_12_Decal_Inst.uasset")).Asset;
			string text8 = Encoding.ASCII.GetString(item2);
			Require(text8.Contains("f16c_12_Decal_Inst"), "stock decal was not renamed onto the new slot");
			Require(text8.Contains("f16c_12_Inst"), "stock decal still parents to the source body material");
			Require(!text8.Contains("f16c_02_"), "source slot survived inside the copied decal");
			Directory.Delete(text7, recursive: true);
			string text9 = Path.Combine(Path.GetTempPath(), "SkinSlotsBody_" + Guid.NewGuid().ToString("N"));
			SkinSlotBuilder.StageStockBody(pawnRoot, text9, "f16c", "02", "12", null);
			byte[] item3 = AcePackage.ReadPlain(Path.Combine(text9, "Nimbus", "Content", "Vehicles", "Aircraft", "f16c", "12", "f16c_12_Inst.uasset")).Asset;
			string text10 = Encoding.ASCII.GetString(item3);
			Require(text10.Contains("f16c_12_Inst"), "stock body was not renamed onto the new slot");
			Require(!text10.Contains("f16c_02_D"), "source diffuse survived inside the copied body");
			Require(!text10.Contains("f16c_02_MREC"), "source roughness survived inside the copied body");
			Directory.Delete(text9, recursive: true);
			string text11 = Path.Combine(Path.GetTempPath(), "SkinSlotsBody35_" + Guid.NewGuid().ToString("N"));
			SkinSlotBuilder.StageStockBody(pawnRoot, text11, "f35c", "02", "06", null);
			string text12 = Encoding.ASCII.GetString(AcePackage.ReadPlain(Path.Combine(text11, "Nimbus", "Content", "Vehicles", "Aircraft", "f35c", "06", "f35c_06_Inst.uasset")).Asset);
			Require(text12.Contains("f35c_06_D"), "F-35C diffuse was not moved to the new slot");
			Require(text12.Contains("f35c_06_MREC"), "F-35C roughness was not moved to the new slot");
			Require(text12.Contains("f35c_06_N"), "F-35C normal was not copied onto the new slot");
			Require(!text12.Contains("f35c_02_N"), "F-35C normal still points at another slot");
			Directory.Delete(text11, recursive: true);
			string text16 = Path.Combine(Path.GetTempPath(), "SkinSlotsBodyChoice_" + Guid.NewGuid().ToString("N"));
			SkinSlotBuilder.StageStockBody(pawnRoot, text16, "f35c", "02", "06", null, replaceExisting: false, useModMaterial: false, materialSlot: "01", useModNormal: false, normalSlot: "00");
			string text17 = Encoding.ASCII.GetString(AcePackage.ReadPlain(Path.Combine(text16, "Nimbus", "Content", "Vehicles", "Aircraft", "f35c", "06", "f35c_06_Inst.uasset")).Asset);
			Require(text17.Contains("f35c_01_D"), "chosen material diffuse was not used");
			Require(text17.Contains("f35c_06_MREC"), "chosen roughness was not copied onto the new slot");
			Require(text17.Contains("f35c_06_N"), "chosen normal was not copied onto the new slot");
			Require(!text17.Contains("f35c_01_MREC"), "roughness still points at the source slot");
			Require(!text17.Contains("f35c_00_N"), "normal still points at the source slot");
			Directory.Delete(text16, recursive: true);
			string text18 = Path.Combine(Path.GetTempPath(), "SkinSlotsTex_" + Guid.NewGuid().ToString("N"));
			string savedNormal = PawnLibrary.StockTexture(pawnRoot, "f35c", "02", "N");
			SkinSlotBuilder.StageSavedTexture(pawnRoot, text18, "f35c", "02", "06", "N");
			SkinSlotBuilder.StageSavedTexture(pawnRoot, text18, "f35c", "00", "06", "MREC");
			string copiedNormal = Path.Combine(text18, "Nimbus", "Content", "Vehicles", "Aircraft", "f35c", "06", "f35c_06_N.uasset");
			string copiedMrec = Path.Combine(text18, "Nimbus", "Content", "Vehicles", "Aircraft", "f35c", "06", "f35c_06_MREC.uasset");
			string normalText = Encoding.ASCII.GetString(AcePackage.ReadPlain(copiedNormal).Asset);
			string mrecText = Encoding.ASCII.GetString(AcePackage.ReadPlain(copiedMrec).Asset);
			Require(normalText.Contains("/Game/Vehicles/Aircraft/f35c/06/f35c_06_N"), "copied normal was not renamed onto the new slot");
			Require(!normalText.Contains("f35c_02_N"), "copied normal still names the source slot");
			Require(mrecText.Contains("/Game/Vehicles/Aircraft/f35c/06/f35c_06_MREC"), "copied roughness was not renamed onto the new slot");
			Require(!mrecText.Contains("f35c_00_MREC"), "copied roughness still names the source slot");
			Require(new FileInfo(copiedNormal).Length == new FileInfo(savedNormal).Length, "copying the normal changed the file size");
			Require(File.Exists(Path.ChangeExtension(copiedNormal, ".ubulk")), "copied normal is missing its bulk");
			Require(File.Exists(Path.ChangeExtension(copiedMrec, ".ubulk")), "copied roughness is missing its bulk");
			Directory.Delete(text18, recursive: true);
			string templateSlot3;
			string template35 = PawnLibrary.TemplatePath(pawnRoot, "f35c", "02", out templateSlot3);
			string pawn35 = Path.Combine(Path.GetTempPath(), "SkinSlotsPawn35_" + Guid.NewGuid().ToString("N"), "AcePlayerPawn_f35c_s06.uasset");
			SkinSlotBuilder.StagePawn(template35, pawn35, "f35c", templateSlot3, "06");
			string text15 = Encoding.ASCII.GetString(AcePackage.ReadPlain(pawn35).Asset);
			Require(text15.Contains("f35c_02_CP_Inst"), "F-35C cockpit reference was changed");
			Require(!text15.Contains("f35c_06_CP_Inst"), "F-35C cockpit was moved onto the new slot");
			Require(!text15.Contains("f35c_00_CP_Inst"), "F-35C cockpit was moved onto the base jet");
			Directory.Delete(Path.GetDirectoryName(pawn35)!, recursive: true);
			string? dam = SkinSlotBuilder.MapPawn("/Game/Vehicles/Aircraft/f15c/11/f15c_11_Dam_Inst", "f15c", "11", "14");
			Require(dam != null && dam.Contains("f15c_14_Dam_Inst") && dam.Contains("/f15c/14/"), "damage instance was not moved onto the new slot");
			Require(SkinSlotBuilder.MapPawn("f15c_11_CP_Inst", "f15c", "11", "14") == null, "cockpit instance was retargeted");
			string unpack = @"D:\Modding\ACE7\UnrealPak-Enhanced-v2.0x-ZR1009\Engine\Binaries\Win64\Unpack";
			string elysia = Path.Combine(unpack, "CFA-44_Skin6_Elysia_P");
			string elysiaBody = Path.Combine(elysia, "Nimbus", "Content", "Vehicles", "Aircraft", "fa44", "05", "fa44_05_Inst.uasset");
			string sharedStage = Path.Combine(Path.GetTempPath(), "SkinSlotsShared_" + Guid.NewGuid().ToString("N"));
			SkinSlotBuilder.StageReplacer(elysia, sharedStage, "fa44", "05", "08", true, false, null, null, SlotTextureLayout.Shared, null);
			string sharedInst = Path.Combine(sharedStage, "Nimbus", "Content", "Vehicles", "Aircraft", "fa44", "08", "fa44_08_Inst.uasset");
			string sharedText = Encoding.ASCII.GetString(AcePackage.ReadPlain(sharedInst).Asset);
			Require(sharedText.Contains("fa44_08_Inst"), "shared instance was not renamed");
			Require(sharedText.Contains("fa44_pe_D") || sharedText.Contains("fa44_PE_D"), "shared diffuse was retargeted");
			Require(sharedText.Contains("fa44_pe_MREC") || sharedText.Contains("fa44_PE_MREC"), "shared roughness was retargeted");
			Require(sharedText.Contains("fa44_00_N"), "shared normal was moved off the base slot");
			Require(!sharedText.Contains("fa44_08_N"), "shared normal was copied onto the new slot");
			Require(!sharedText.Contains("fa44_05_Inst"), "shared instance still names the source slot");
			SkinSlotBuilder.StageStockDecal(pawnRoot, sharedStage, "fa44", "05", "08", null, replaceExisting: false, layout: SlotTextureLayout.Shared, sharedBody: elysiaBody);
			string sharedDecal = Encoding.ASCII.GetString(AcePackage.ReadPlain(Path.Combine(sharedStage, "Nimbus", "Content", "Vehicles", "Aircraft", "fa44", "08", "fa44_08_Decal_Inst.uasset")).Asset);
			Require(sharedDecal.Contains("fa44_08_Decal_Inst"), "shared decal was not renamed");
			Require(sharedDecal.Contains("fa44_08_Inst"), "shared decal does not parent the new instance");
			Require(sharedDecal.Contains("fa44_PE_D") || sharedDecal.Contains("fa44_pe_D"), "decal did not follow the instance diffuse");
			Require(sharedDecal.Contains("fa44_PE_MREC") || sharedDecal.Contains("fa44_pe_MREC"), "decal did not follow the instance roughness");
			Require(sharedDecal.Contains("fa44_00_N"), "decal did not keep the instance normal");
			Require(!sharedDecal.Contains("fa44_05_D"), "decal still uses the source diffuse");
			Require(!sharedDecal.Contains("fa44_00_MREC"), "decal still uses the base roughness");
			Directory.Delete(sharedStage, recursive: true);
			string jousters = Path.Combine(unpack, "03_F15C Jousters Variant 2 - Slot 12_P");
			string joustStage = Path.Combine(Path.GetTempPath(), "SkinSlotsJoust_" + Guid.NewGuid().ToString("N"));
			SkinSlotBuilder.StageReplacer(jousters, joustStage, "f15c", "11", "14", true, false, null, null, SlotTextureLayout.Shared, null);
			string damInst = Path.Combine(joustStage, "Nimbus", "Content", "Vehicles", "Aircraft", "f15c", "14", "f15c_14_Dam_Inst.uasset");
			string damText = Encoding.ASCII.GetString(AcePackage.ReadPlain(damInst).Asset);
			Require(File.Exists(damInst), "damage instance was not copied");
			Require(damText.Contains("f15c_14_Dam_Inst"), "damage instance was not renamed");
			Require(damText.Contains("lud-jousters"), "damage instance lost the shared texture folder");
			Require(damText.Contains("f15c_00_N"), "damage instance lost the base normal");
			Require(!damText.Contains("f15c_14_N"), "damage instance normal was moved onto the new slot");
			Require(!damText.Contains("f15c_11_Dam_Inst"), "damage instance still names the source slot");
			string joustBody = Encoding.ASCII.GetString(AcePackage.ReadPlain(Path.Combine(joustStage, "Nimbus", "Content", "Vehicles", "Aircraft", "f15c", "14", "f15c_14_Inst.uasset")).Asset);
			Require(joustBody.Contains("lud-jou2"), "body instance lost the shared diffuse");
			Require(joustBody.Contains("f15c-blank-MREC"), "body instance lost the shared roughness");
			Directory.Delete(joustStage, recursive: true);
			string bulkStage = Path.Combine(Path.GetTempPath(), "SkinSlotsBulk_" + Guid.NewGuid().ToString("N"));
			SkinSlotBuilder.StageStockBody(pawnRoot, bulkStage, "pkfa", "01", "08", null, layout: SlotTextureLayout.BulkOnly, materialSlot: "08", normalSlot: "00");
			string bulkInstPath = Path.Combine(bulkStage, "Nimbus", "Content", "Vehicles", "Aircraft", "pkfa", "08", "pkfa_08_Inst.uasset");
			Require(PlainHeader(bulkInstPath), "bulk instance was saved encrypted");
			string bulkBody = Encoding.ASCII.GetString(AcePackage.ReadPlain(bulkInstPath).Asset);
			Require(bulkBody.Contains("pkfa_08_D"), "bulk diffuse was not aimed at the new slot");
			Require(bulkBody.Contains("pkfa_08_MREC"), "bulk roughness was not aimed at the cloned header");
			Require(bulkBody.Contains("pkfa_00_N"), "bulk normal left the original slot");
			Require(!bulkBody.Contains("pkfa_01_MREC"), "bulk roughness still names the original slot");
			Require(!bulkBody.Contains("pkfa_01_D"), "bulk diffuse still names the original slot");
			string savedMrec = PawnLibrary.StockTexture(pawnRoot, "pkfa", "01", "MREC");
			SkinSlotBuilder.StageSavedTexture(pawnRoot, bulkStage, "pkfa", "01", "08", "MREC", null, copyBulk: false);
			string clonedMrec = Path.Combine(bulkStage, "Nimbus", "Content", "Vehicles", "Aircraft", "pkfa", "08", "pkfa_08_MREC.uasset");
			Require(PlainHeader(clonedMrec), "roughness header was saved encrypted");
			Require(File.Exists(clonedMrec), "roughness header was not cloned");
			Require(File.Exists(Path.ChangeExtension(clonedMrec, ".uexp")), "roughness header export was not cloned");
			Require(!File.Exists(Path.ChangeExtension(clonedMrec, ".ubulk")), "stock roughness bulk was packed with the header");
			Require(new FileInfo(savedMrec).Length == new FileInfo(clonedMrec).Length, "cloned roughness header changed size");
			Require(new UAssetAPI.UAsset(clonedMrec, UAssetAPI.UnrealTypes.EngineVersion.VER_UE4_18).VerifyBinaryEquality(), "roughness header does not round-trip");
			string working = @"D:\Modding\ACE7\UnrealPak-Enhanced-v2.0x-ZR1009\Engine\Binaries\Win64\SkinReSlotter_Su-57_AK-12_Skin_Skin9_Working_P\Nimbus\Content\Vehicles\Aircraft\pkfa\08";
			if (Directory.Exists(working))
			{
				Require(SameBytes(clonedMrec, Path.Combine(working, "pkfa_08_MREC.uasset")), "cloned roughness header does not match the working file");
				Require(SameBytes(Path.ChangeExtension(clonedMrec, ".uexp"), Path.Combine(working, "pkfa_08_MREC.uexp")), "cloned roughness export does not match the working file");
				SkinSlotBuilder.StageSavedTexture(pawnRoot, bulkStage, "pkfa", "01", "08", "D", null, copyBulk: false);
				string clonedDiffuse = Path.Combine(bulkStage, "Nimbus", "Content", "Vehicles", "Aircraft", "pkfa", "08", "pkfa_08_D.uasset");
				Require(SameBytes(clonedDiffuse, Path.Combine(working, "pkfa_08_D.uasset")), "cloned diffuse header does not match the working file");
				Require(SameBytes(Path.ChangeExtension(clonedDiffuse, ".uexp"), Path.Combine(working, "pkfa_08_D.uexp")), "cloned diffuse export does not match the working file");
			}
			SkinSlotBuilder.StageLooseBulk(Path.Combine(unpack, "su57ak12"), bulkStage, "pkfa", "01", "08");
			Require(File.Exists(Path.Combine(bulkStage, "Nimbus", "Content", "Vehicles", "Aircraft", "pkfa", "08", "pkfa_08_D.ubulk")), "diffuse bulk was not placed on the new slot");
			Require(File.Exists(Path.ChangeExtension(clonedMrec, ".ubulk")), "roughness bulk was not placed beside the cloned header");
			Require(!File.Exists(Path.Combine(bulkStage, "Nimbus", "Content", "Vehicles", "Aircraft", "pkfa", "01", "pkfa_01_MREC.ubulk")), "roughness bulk stayed on the original slot");
			SkinSourcePreview bulkPreview = SkinSlotBuilder.Inspect(Path.Combine(unpack, "su57ak12"), null, pawnRoot, null);
			Require(bulkPreview.BulkOnly && bulkPreview.ShipsDiffuse && !bulkPreview.ShipsDiffuseUasset && bulkPreview.ShipsMrec && !bulkPreview.ShipsMrecUasset && !bulkPreview.ShipsNormalFile, "ubulk-only pak contents were misread");
			Directory.Delete(bulkStage, recursive: true);
			Require(PawnLibrary.FindShippedPawn(new[] { "Skin/AcePlayerPawn_f15c_s11.uasset", "f15c_11_Inst.uasset" }, "f15c") == "Skin/AcePlayerPawn_f15c_s11.uasset", "shipped pawn was not found");
			SkinSourcePreview elysiaPreview = SkinSlotBuilder.Inspect(elysia, null, pawnRoot, null);
			Require(elysiaPreview.SharedTextures, "Elysia was not treated as a shared texture folder");
			Require(!elysiaPreview.ShipsDecal, "Elysia was treated as shipping a decal");
			Require(elysiaPreview.MaterialFromShared, "Elysia roughness was treated as a numbered slot");
			Require(!elysiaPreview.NormalFromShared && elysiaPreview.NormalReferenceSlot == "00", "Elysia normal was not kept on slot 00");
			string instancesOnly = Path.Combine(Path.GetTempPath(), "SkinSlotsInstOnly_" + Guid.NewGuid().ToString("N"));
			string instanceSlot = Path.Combine(instancesOnly, "Nimbus", "Content", "Vehicles", "Aircraft", "f16c", "02");
			Directory.CreateDirectory(instanceSlot);
			File.WriteAllBytes(Path.Combine(instanceSlot, "f16c_02_Inst.uasset"), new byte[] { 1, 2, 3, 4 });
			File.WriteAllBytes(Path.Combine(instanceSlot, "f16c_02_Inst.uexp"), new byte[] { 1 });
			File.WriteAllBytes(Path.Combine(instanceSlot, "f16c_02_Decal_Inst.uasset"), new byte[] { 1, 2, 3, 4 });
			File.WriteAllBytes(Path.Combine(instanceSlot, "f16c_02_Decal_Inst.uexp"), new byte[] { 1 });
			string pawnDir = Path.Combine(instancesOnly, "Nimbus", "Content", "Blueprint", "Player", "Pawn", "Skin");
			Directory.CreateDirectory(pawnDir);
			File.WriteAllBytes(Path.Combine(pawnDir, "AcePlayerPawn_f16c_s02.uasset"), new byte[] { 1, 2, 3, 4 });
			File.WriteAllBytes(Path.Combine(pawnDir, "AcePlayerPawn_f16c_s02.uexp"), new byte[] { 1 });
			SkinSourcePreview decalPreview = SkinSlotBuilder.Inspect(instancesOnly, null, pawnRoot, null);
			Require(decalPreview.SharedTextures, "an instance, a decal, and a skin pawn were not treated as instance-only");
			Require(decalPreview.ShipsDecal, "the shipped decal was not noticed");
			Require(decalPreview.ShippedPawn, "the shipped skin pawn was not kept");
			Require(decalPreview.ShipsInstance && !decalPreview.ShipsDiffuse && !decalPreview.ShipsMrec && !decalPreview.ShipsNormalFile, "instance-only pak contents were misread");
			string rcaf = Path.Combine(unpack, "F-35C -RCAF Pine- Slot 03_P");
			string rcafStage = Path.Combine(Path.GetTempPath(), "SkinSlotsRcaf_" + Guid.NewGuid().ToString("N"));
			SkinSlotBuilder.StageReplacer(rcaf, rcafStage, "f35c", "02", "06", true, false, null, null, SlotTextureLayout.Standard, null);
			Require(ShortTextureName(Path.Combine(rcafStage, "Nimbus", "Content", "Vehicles", "Aircraft", "f35c", "06", "f35c_06_D.uasset"), "/Game/f35c_06_D"), "RCAF diffuse name was expanded to a full path");
			Require(ShortTextureName(Path.Combine(rcafStage, "Nimbus", "Content", "Vehicles", "Aircraft", "f35c", "06", "f35c_06_MREC.uasset"), "/Game/f35c_06_MREC"), "RCAF roughness name was expanded to a full path");
			Directory.Delete(rcafStage, recursive: true);
			File.WriteAllBytes(Path.Combine(instanceSlot, "f16c_02_D.uasset"), new byte[] { 1, 2, 3, 4 });
			SkinSourcePreview texturedPreview = SkinSlotBuilder.Inspect(instancesOnly, null, pawnRoot, null);
			Require(!texturedPreview.SharedTextures, "a pak that ships a diffuse was treated as instance-only");
			Require(texturedPreview.ShipsDiffuse && texturedPreview.ShipsDiffuseUasset && !texturedPreview.ShipsMrec, "diffuse was not listed apart from roughness");
			Directory.Delete(instancesOnly, recursive: true);
			string locStage = Path.Combine(Path.GetTempPath(), "SkinSlotsLoc_" + Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(locStage);
			Dictionary<char, SkinLocale> languages = new Dictionary<char, SkinLocale>
			{
				['C'] = new SkinLocale { Name = "Pin français" },
				['F'] = new SkinLocale { Description = "日本語の説明" }
			};
			MountedSkin.Cook(locStage, null, PawnLibrary.StockSkinTable(pawnRoot), PawnLibrary.StockLocalizationDirectory(pawnRoot), "f16c", 40, "/Game/Blueprint/Player/Pawn/Skin/AcePlayerPawn_f16c_s40.AcePlayerPawn_f16c_s40_C", "Pine", "English description", null, languages);
			string locDir = Path.Combine(locStage, "Nimbus", "Content", "Localization", "Game");
			Require(Localized(locDir, 'A', "AircraftSkintype_Name_f16c_DLC35") == "Pine", "English name was not written");
			Require(Localized(locDir, 'B', "AircraftSkintype_Name_f16c_DLC35") == "Pine", "English name was not copied to Traditional Chinese");
			Require(Localized(locDir, 'C', "AircraftSkintype_Name_f16c_DLC35") == "Pin français", "French name kept the English name");
			Require(Localized(locDir, 'F', "AircraftSkintype_Name_f16c_DLC35") == "Pine", "Japanese name did not keep the English name");
			Require(Localized(locDir, 'A', "AircraftSkin_Description_f16c_40") == "English description", "English description was not written");
			Require(Localized(locDir, 'C', "AircraftSkin_Description_f16c_40") == "English description", "French description did not keep the English description");
			Require(Localized(locDir, 'F', "AircraftSkin_Description_f16c_40") == "日本語の説明", "Japanese description kept the English description");
			Directory.Delete(locStage, recursive: true);
		}
		finally
		{
			string directoryName = Path.GetDirectoryName(text2);
			if (directoryName != null && Directory.Exists(directoryName))
			{
				Directory.Delete(directoryName, recursive: true);
			}
		}
	}

	private static string Localized(string directory, char letter, string key)
	{
		int index = new CmnFile(Path.Combine(directory, "Cmn.dat")).GetVariableStringNumber(key);
		DatFile dat = new DatFile(Path.Combine(directory, letter + ".dat"));
		if (index < 0 || index >= dat.Strings.Count)
		{
			return "";
		}
		return dat.Strings[index].TrimEnd('\0');
	}

	private static bool ShortTextureName(string uassetPath, string packageName)
	{
		UAssetAPI.UAsset asset = new UAssetAPI.UAsset(uassetPath, UAssetAPI.UnrealTypes.EngineVersion.VER_UE4_18);
		bool found = false;
		foreach (UAssetAPI.UnrealTypes.FString name in asset.GetNameMapIndexList())
		{
			if (name.Value.Equals(packageName, StringComparison.Ordinal))
			{
				found = true;
			}
			if (name.Value.Contains("/Vehicles/Aircraft/", StringComparison.OrdinalIgnoreCase) && name.Value.Contains(Path.GetFileNameWithoutExtension(uassetPath), StringComparison.OrdinalIgnoreCase))
			{
				return false;
			}
		}
		return found;
	}

	private static bool SameBytes(string left, string right)
	{
		byte[] a = File.ReadAllBytes(left);
		byte[] b = File.ReadAllBytes(right);
		return a.AsSpan().SequenceEqual(b);
	}

	private static bool PlainHeader(string path)
	{
		using FileStream stream = File.OpenRead(path);
		return stream.ReadByte() == 0xC1 && stream.ReadByte() == 0x83 && stream.ReadByte() == 0x2A && stream.ReadByte() == 0x9E;
	}

	private static void Require(bool condition, string message)
	{
		if (!condition)
		{
			throw new InvalidOperationException(message);
		}
	}
}
