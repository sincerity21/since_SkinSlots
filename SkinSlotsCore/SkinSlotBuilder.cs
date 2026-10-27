using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using SkinConverterCore.Models;
using SkinConverterCore.Services;

namespace SkinSlotsCore;

public static class SkinSlotBuilder
{
	public static SkinSourcePreview Inspect(string sourcePath, string? unrealPakExe, string? pawnRoot, string? modsDirectory = null, Action<string>? log = null)
	{
		if (string.IsNullOrWhiteSpace(pawnRoot))
		{
			pawnRoot = "D:\\Modding\\ACE7\\UmodelAC7 + Decryption\\UmodelSaved\\Game\\Blueprint\\Player\\Pawn";
		}
		return UsingSource(sourcePath, unrealPakExe, log, delegate(string sourceRoot)
		{
			DetectedSkinInfo detectedSkinInfo = SkinSlotDetector.Detect(sourceRoot, log);
			string plane = detectedSkinInfo.PlaneStringId.ToLowerInvariant();
			string sourceSlotIndex = detectedSkinInfo.SourceSlotIndex;
			if (sourceSlotIndex.Length != 2)
			{
				throw new InvalidOperationException("Source slot '" + sourceSlotIndex + "' is not a two-digit folder index.");
			}
			string[] files = Directory.GetFiles(sourceRoot, "*.*", SearchOption.AllDirectories);
			HashSet<string> hashSet = PawnLibrary.ShippedRoles(files, plane, sourceSlotIndex);
			bool usesStockBody = !PawnLibrary.ShipsBody(hashSet) && PawnLibrary.TryStockBody(pawnRoot, plane, sourceSlotIndex) != null;
			string? pakInstance = files.FirstOrDefault((string file) => Path.GetFileName(file).Equals(plane + "_" + sourceSlotIndex + "_Inst.uasset", StringComparison.OrdinalIgnoreCase));
			string? referenceInstance = pakInstance ?? PawnLibrary.TryStockBody(pawnRoot, plane, sourceSlotIndex);
			string? materialReference = null;
			string? normalReference = null;
			string? referenceText = null;
			if (referenceInstance != null)
			{
				referenceText = Encoding.ASCII.GetString(AcePackage.ReadPlain(referenceInstance).Asset);
				materialReference = ReferencedSlot(referenceText, plane, "_D");
				normalReference = ReferencedSlot(referenceText, plane, "_N");
			}
			string? shippedPawn = PawnLibrary.FindShippedPawn(files, plane);
			bool hasTemplatePawn = shippedPawn != null;
			string templatePawn = shippedPawn == null ? "" : Path.GetFileNameWithoutExtension(shippedPawn);
			if (!hasTemplatePawn)
			{
				try
				{
					string templateSlot;
					string path = PawnLibrary.TemplatePath(pawnRoot, plane, sourceSlotIndex, out templateSlot);
					templatePawn = Path.GetFileNameWithoutExtension(path);
					hasTemplatePawn = true;
				}
				catch (FileNotFoundException)
				{
					templatePawn = "";
				}
			}
			SlotTextureLayout layout = ClassifyLayout(files, plane, pakInstance);
			bool materialFromShared = false;
			bool normalFromShared = false;
			string? sharedMaterialSlot = null;
			if (layout == SlotTextureLayout.Shared && pakInstance != null && referenceText != null)
			{
				ReadSharedTargets(referenceText, plane, out materialFromShared, out sharedMaterialSlot, out normalFromShared, out string? sharedNormalSlot);
				if (!normalFromShared && sharedNormalSlot != null)
				{
					normalReference = sharedNormalSlot;
				}
			}
			PlaneSkins planeSkins = PawnLibrary.Describe(pawnRoot, plane);
			using InheritedSources inheritedSources = ModFolder.Open(modsDirectory, pawnRoot, unrealPakExe, log);
			MountedSkin.PlaneTableInfo planeTableInfo = MountedSkin.ReadPlane(inheritedSources.TablePath, plane);
			int num = Math.Max(planeSkins.LastSuffix, planeTableInfo.MaxSkinNo);
			return new SkinSourcePreview
			{
				Plane = plane,
				SourceSkinNumber = detectedSkinInfo.SourceSlotNumber,
				SourceSlotIndex = sourceSlotIndex,
				Roles = string.Join(", ", hashSet.OrderBy<string, string>((string role) => role, StringComparer.OrdinalIgnoreCase)),
				HasHangarMaterial = PawnLibrary.HasHangarMaterial(hashSet) || usesStockBody,
				UsesStockBody = usesStockBody,
				ShipsMaterial = PawnLibrary.ShipsMaterialTexture(files, plane),
				ShipsNormal = PawnLibrary.ShipsNormalTexture(files, plane),
				MaterialReferenceSlot = materialReference,
				NormalReferenceSlot = normalReference,
				TemplatePawn = templatePawn,
				FileCount = files.Length,
				StockSkinCount = planeSkins.SkinCount,
				LastStockSuffix = planeSkins.LastSuffix,
				NextSuffix = num + 1,
				PlaneLabel = MountedSkin.PlaneLabel(inheritedSources.LocalizationDirectory, plane),
				TableFromMods = inheritedSources.TableFromMods,
				SkinNos = planeTableInfo.SkinNos,
				BulkOnly = layout == SlotTextureLayout.BulkOnly,
				SharedTextures = layout == SlotTextureLayout.Shared,
				HasTemplatePawn = hasTemplatePawn,
				ShippedPawn = shippedPawn != null,
				ShipsDecal = hashSet.Any((string role) => role.Contains("Decal", StringComparison.OrdinalIgnoreCase)),
				ShipsInstance = PawnLibrary.ShipsBody(hashSet),
				ShipsDiffuse = HasPayload(files, (string name) => name.EndsWith("_D", StringComparison.OrdinalIgnoreCase)),
				ShipsDiffuseUasset = HasPayload(files, (string name) => name.EndsWith("_D", StringComparison.OrdinalIgnoreCase), ".uasset"),
				ShipsMrec = HasPayload(files, (string name) => name.Contains("MREC", StringComparison.OrdinalIgnoreCase)),
				ShipsMrecUasset = HasPayload(files, (string name) => name.Contains("MREC", StringComparison.OrdinalIgnoreCase), ".uasset"),
				ShipsNormalFile = HasPayload(files, (string name) => name.EndsWith("_N", StringComparison.OrdinalIgnoreCase)),
				ShipsNormalUasset = HasPayload(files, (string name) => name.EndsWith("_N", StringComparison.OrdinalIgnoreCase), ".uasset"),
				MaterialFromShared = materialFromShared,
				NormalFromShared = normalFromShared,
				SharedMaterialSlot = sharedMaterialSlot
			};
		});
	}

	public static SkinSlotBuildResult Build(SkinSlotBuildRequest request, Action<string>? log = null)
	{
		return UsingSource(request.SourcePath, request.UnrealPakExe, log, delegate(string sourceRoot)
		{
			DetectedSkinInfo detectedSkinInfo = SkinSlotDetector.Detect(sourceRoot, log);
			string text = detectedSkinInfo.PlaneStringId.ToLowerInvariant();
			string sourceSlotIndex = detectedSkinInfo.SourceSlotIndex;
			if (sourceSlotIndex.Length != 2)
			{
				throw new InvalidOperationException("Source slot '" + sourceSlotIndex + "' is not a two-digit folder index.");
			}
			string[] sourceFiles = Directory.GetFiles(sourceRoot, "*.*", SearchOption.AllDirectories);
			HashSet<string> hashSet = PawnLibrary.ShippedRoles(sourceFiles, text, sourceSlotIndex);
			bool shipsMaterial = PawnLibrary.ShipsMaterialTexture(sourceFiles, text);
			bool shipsNormal = PawnLibrary.ShipsNormalTexture(sourceFiles, text);
			string pawnRoot = (string.IsNullOrWhiteSpace(request.PawnRoot) ? "D:\\Modding\\ACE7\\UmodelAC7 + Decryption\\UmodelSaved\\Game\\Blueprint\\Player\\Pawn" : request.PawnRoot);
			string? pakInstance = sourceFiles.FirstOrDefault((string file) => Path.GetFileName(file).Equals(text + "_" + sourceSlotIndex + "_Inst.uasset", StringComparison.OrdinalIgnoreCase));
			SlotTextureLayout layout = ClassifyLayout(sourceFiles, text, pakInstance);
			string bodySlot = ChosenSlot(request.BodySlot, sourceSlotIndex);
			string decalSlot = ChosenSlot(request.DecalSlot, sourceSlotIndex);
			if (layout == SlotTextureLayout.Shared)
			{
				bodySlot = sourceSlotIndex;
				decalSlot = sourceSlotIndex;
			}
			bool useModMaterial = UseModTexture(request.MaterialSlot, shipsMaterial);
			bool useModNormal = UseModTexture(request.NormalSlot, shipsNormal);
			string materialSlot = useModMaterial ? sourceSlotIndex : ChosenSlot(request.MaterialSlot, sourceSlotIndex);
			string? normalSlot = useModNormal ? null : (string.IsNullOrWhiteSpace(request.NormalSlot) ? null : ChosenSlot(request.NormalSlot, sourceSlotIndex));
			bool usePakBody = PawnLibrary.ShipsBody(hashSet) && bodySlot.Equals(sourceSlotIndex, StringComparison.OrdinalIgnoreCase);
			bool usePakDecal = hashSet.Any((string role) => role.Contains("Decal", StringComparison.OrdinalIgnoreCase)) && decalSlot.Equals(sourceSlotIndex, StringComparison.OrdinalIgnoreCase);
			if (!usePakBody)
			{
				PawnLibrary.StockBody(pawnRoot, text, bodySlot);
			}
			if (!usePakDecal && layout == SlotTextureLayout.Standard)
			{
				PawnLibrary.StockDecal(pawnRoot, text, decalSlot);
			}
			string? shippedPawn = PawnLibrary.FindShippedPawn(sourceFiles, text);
			string templateSlot;
			string text2;
			if (shippedPawn != null && PawnLibrary.TryPawnSuffix(shippedPawn, text, out templateSlot))
			{
				text2 = shippedPawn;
			}
			else
			{
				text2 = PawnLibrary.TemplatePath(pawnRoot, text, sourceSlotIndex, out templateSlot);
			}
			PlaneSkins planeSkins = PawnLibrary.Describe(pawnRoot, text);
			if (request.UnrealPakExe == null || !File.Exists(request.UnrealPakExe))
			{
				throw new FileNotFoundException("UnrealPak.exe is required to pack the new slot.", request.UnrealPakExe);
			}
			using InheritedSources inheritedSources = ModFolder.Open(request.ModsDirectory, pawnRoot, request.UnrealPakExe, log);
			MountedSkin.PlaneTableInfo planeTableInfo = MountedSkin.ReadPlane(inheritedSources.TablePath, text);
			int num = Math.Max(planeSkins.LastSuffix, planeTableInfo.MaxSkinNo);
			int num2 = HangarToSuffix(request.Slot) ?? (num + 1);
			if (num2 <= planeSkins.LastSuffix || num2 > 99)
			{
				throw new InvalidOperationException($"Slot {num2 + 1} is outside the free range {planeSkins.LastSuffix + 2}-{100}. Original skins use the slots through {planeSkins.LastSuffix + 1}.");
			}
			string text3 = num2.ToString("D2");
			int num3 = num2 + 1;
			string text4 = (string.IsNullOrWhiteSpace(request.DisplayName) ? "Skin" : request.DisplayName.Trim());
			string text5 = MountedSkin.PlaneLabel(inheritedSources.LocalizationDirectory, text);
			string text6 = "AcePlayerPawn_" + text + "_s" + text3;
			string text7 = $"/Game/Blueprint/Player/Pawn/Skin/{text6}.{text6}_C";
			log?.Invoke(shippedPawn == null
				? $"Slot {detectedSkinInfo.SourceSlotNumber} uses stock pawn s{templateSlot}. Slot {num3} uses pawn s{text3}. Shipped roles: {string.Join(", ", hashSet)}"
				: $"Slot {detectedSkinInfo.SourceSlotNumber} uses the pawn shipped in the mod, s{templateSlot}. Slot {num3} uses pawn s{text3}. Shipped roles: {string.Join(", ", hashSet)}");
			string text8 = ContentDirectory(request);
			string text9 = PakFileName(text5, text4, num3);
			bool num4 = !string.IsNullOrWhiteSpace(request.OutputPakPath);
			string text10 = (num4 ? null : ModFolder.FindPak(text8, text9));
			string text11 = (num4 ? OutputPathHelper.EnsurePakPatchSuffix(request.OutputPakPath) : (text10 ?? Path.Combine(text8, text9)));
			string text12 = null;
			string text13 = null;
			try
			{
				if (text10 != null)
				{
					log?.Invoke("Skin pak already in the mods folder: " + Path.GetFileName(text10) + ". Leaving its textures and pawn alone.");
				}
				else
				{
					text12 = Path.Combine(Path.GetTempPath(), "SkinSlotsStage_" + Guid.NewGuid().ToString("N"));
					Directory.CreateDirectory(text12);
					if (layout == SlotTextureLayout.Shared)
					{
						log?.Invoke("Instance uses a shared texture folder. Those textures stay in that folder, and the decal follows the instance.");
					}
					else if (layout == SlotTextureLayout.BulkOnly)
					{
						materialSlot = text3;
						string? bulkReference = pakInstance ?? PawnLibrary.TryStockBody(pawnRoot, text, sourceSlotIndex);
						normalSlot = bulkReference == null ? sourceSlotIndex : ReferencedSlot(Encoding.ASCII.GetString(AcePackage.ReadPlain(bulkReference).Asset), text, "_N") ?? sourceSlotIndex;
						log?.Invoke("Bulk-only pak. Diffuse and roughness headers are copied from slot " + sourceSlotIndex + " onto slot " + text3 + " and saved decrypted, with the ubulks beside them. The normal stays on slot " + normalSlot + ".");
					}
					else
					{
						log?.Invoke(useModMaterial ? "Material textures come from the mod." : "Roughness is copied from slot " + materialSlot + ".");
						log?.Invoke(useModNormal ? "Normal comes from the mod." : "Normal is copied from slot " + (normalSlot ?? sourceSlotIndex) + ".");
					}
					StageReplacer(sourceRoot, text12, text, sourceSlotIndex, text3, useModMaterial, useModNormal, materialSlot, normalSlot, layout, log);
					if (layout == SlotTextureLayout.BulkOnly)
					{
						StageSavedTexture(pawnRoot, text12, text, sourceSlotIndex, text3, "D", log, copyBulk: false);
						StageSavedTexture(pawnRoot, text12, text, sourceSlotIndex, text3, "MREC", log, copyBulk: false);
						StageLooseBulk(sourceRoot, text12, text, sourceSlotIndex, text3, log);
					}
					if (layout == SlotTextureLayout.Standard && !useModMaterial)
					{
						StageSavedTexture(pawnRoot, text12, text, materialSlot, text3, "MREC", log);
					}
					if (layout == SlotTextureLayout.Standard && !useModNormal)
					{
						StageSavedTexture(pawnRoot, text12, text, normalSlot ?? sourceSlotIndex, text3, "N", log);
					}
					StageStockBody(pawnRoot, text12, text, bodySlot, text3, log, !usePakBody && PawnLibrary.ShipsBody(hashSet), useModMaterial, materialSlot, useModNormal, normalSlot, layout);
					try
					{
						StageStockDecal(pawnRoot, text12, text, decalSlot, text3, log, !usePakDecal && hashSet.Any((string role) => role.Contains("Decal", StringComparison.OrdinalIgnoreCase)), useModMaterial, materialSlot, useModNormal, normalSlot, layout, pakInstance);
					}
					catch (FileNotFoundException ex) when (layout != SlotTextureLayout.Standard)
					{
						log?.Invoke(ex.Message);
					}
					string destUasset = Path.Combine(text12, "Nimbus", "Content", "Blueprint", "Player", "Pawn", "Skin", text6 + ".uasset");
					StagePawn(text2, destUasset, text, templateSlot, text3, log);
					PakService.CreatePak(request.UnrealPakExe, text12, text11, log);
					log?.Invoke("Skin pak: " + Path.GetFileName(text11));
				}
				text13 = Path.Combine(Path.GetTempPath(), "SkinSlotsData_" + Guid.NewGuid().ToString("N"));
				Directory.CreateDirectory(text13);
				ModFolder.WriteMarker(text13);
				string text14 = DataDirectory(request, text11);
				CookedSkinRow cookedSkinRow = MountedSkin.Cook(text13, PaksDirectory(text14), inheritedSources.TablePath, inheritedSources.LocalizationDirectory, text, num2, text7, text4, request.Description?.Trim() ?? "", log, request.Languages);
				string text15 = inheritedSources.DataPakPath ?? Path.Combine(text14, ModFolder.DataPakFileName(cookedSkinRow.MountTildes));
				PakService.CreatePak(request.UnrealPakExe, text13, text15, log);
				ModFolder.RemoveOlderDataPaks(text14, text15);
				ModFolder.Invalidate(inheritedSources.TablePak);
				ModFolder.Invalidate(inheritedSources.LocalizationPak);
				ModFolder.Invalidate(text15);
				log?.Invoke((inheritedSources.DataPakPath == null) ? ("Table pak mounts after every other SkinDataTable: " + Path.GetFileName(text15)) : ("Updated the existing data pak: " + Path.GetFileName(text15)));
				SkinSlotManifest manifest = new SkinSlotManifest
				{
					Plane = text,
					SkinNo = num2,
					SkinId = cookedSkinRow.SkinId,
					Pawn = text7,
					Name = text4,
					Description = (request.Description?.Trim() ?? ""),
					SortNumber = cookedSkinRow.SortNumber,
					TemplatePawn = Path.GetFileNameWithoutExtension(text2),
					ShippedRoles = string.Join(",", hashSet.OrderBy<string, string>((string role) => role, StringComparer.OrdinalIgnoreCase))
				};
				log?.Invoke($"Assigned {text5} slot {num3} as {cookedSkinRow.RowName}.");
				return new SkinSlotBuildResult
				{
					PakPath = text11,
					DataPakPath = text15,
					ContentSkipped = (text10 != null),
					Manifest = manifest
				};
			}
			finally
			{
				if (text12 != null)
				{
					TryDelete(text12);
				}
				if (text13 != null)
				{
					TryDelete(text13);
				}
			}
		});
	}

	private static T UsingSource<T>(string sourcePath, string? unrealPakExe, Action<string>? log, Func<string, T> action)
	{
		if (!File.Exists(sourcePath) && !Directory.Exists(sourcePath))
		{
			throw new FileNotFoundException("Skin source was not found.", sourcePath);
		}
		string text = null;
		try
		{
			string arg = sourcePath;
			if (File.Exists(sourcePath))
			{
				if (unrealPakExe == null || !File.Exists(unrealPakExe))
				{
					throw new FileNotFoundException("UnrealPak.exe is required to read a .pak.", unrealPakExe);
				}
				text = Path.Combine(Path.GetTempPath(), "SkinSlots_" + Guid.NewGuid().ToString("N"));
				PakService.ExtractPak(unrealPakExe, sourcePath, text, log);
				arg = text;
			}
			return action(arg);
		}
		finally
		{
			if (text != null)
			{
				TryDelete(text);
			}
		}
	}

	public static void StagePawn(string templateUasset, string destUasset, string plane, string templateSlot, string destSlot, Action<string>? log = null)
	{
		(byte[] Asset, byte[]? Export) tuple = AcePackage.ReadPlain(templateUasset);
		byte[] item = tuple.Asset;
		byte[] array = tuple.Export;
		item = StringRewriter.Rewrite(item, (string text) => MapPawn(text, plane, templateSlot, destSlot));
		if (array != null)
		{
			array = StringRewriter.Rewrite(array, (string text) => MapPawn(text, plane, templateSlot, destSlot));
		}
		AcePackage.WritePlain(destUasset, item, array);
		log?.Invoke("Cloned pawn " + Path.GetFileName(destUasset));
	}

	public static string? MapPawn(string text, string plane, string templateSlot, string destSlot)
	{
		string text2 = "AcePlayerPawn_" + plane + "_s" + templateSlot;
		string newValue = "AcePlayerPawn_" + plane + "_s" + destSlot;
		if (text.Contains(text2, StringComparison.OrdinalIgnoreCase))
		{
			string text3 = text;
			while (text3.Contains(text2, StringComparison.OrdinalIgnoreCase))
			{
				text3 = ReplaceOrdinal(text3, text2, newValue);
			}
			return text3;
		}
		string? mapped;
		if (!StringRewriter.TryMaterialRole(text, plane, templateSlot, out string role) || role.Equals("CP_Inst", StringComparison.OrdinalIgnoreCase))
		{
			mapped = null;
		}
		else
		{
			mapped = StringRewriter.RetargetMaterialText(text, plane, templateSlot, destSlot);
		}
		return FinishDam(mapped ?? text, text, plane, templateSlot, destSlot);
	}

	public static void StageStockBody(string pawnRoot, string stageRoot, string plane, string sourceSlot, string destSlot, Action<string>? log, bool replaceExisting = false, bool useModMaterial = true, string? materialSlot = null, bool useModNormal = false, string? normalSlot = null, SlotTextureLayout layout = SlotTextureLayout.Standard)
	{
		string text = Path.Combine(stageRoot, "Nimbus", "Content", "Vehicles", "Aircraft", plane, destSlot);
		string bodyName = plane + "_" + destSlot + "_Inst.uasset";
		if (!replaceExisting && Directory.Exists(text) && Directory.EnumerateFiles(text, "*.uasset").Any((string file) => Path.GetFileName(file).Equals(bodyName, StringComparison.OrdinalIgnoreCase)))
		{
			return;
		}
		string text2 = PawnLibrary.StockBody(pawnRoot, plane, sourceSlot);
		(byte[] Asset, byte[]? Export) tuple = AcePackage.ReadPlain(text2);
		byte[] item = tuple.Asset;
		byte[] array = tuple.Export;
		item = StringRewriter.Rewrite(item, (string text4) => MapCopiedMaterial(text4, plane, sourceSlot, destSlot, useModMaterial, materialSlot, useModNormal, normalSlot, layout, null));
		if (array != null)
		{
			array = StringRewriter.Rewrite(array, (string text4) => MapCopiedMaterial(text4, plane, sourceSlot, destSlot, useModMaterial, materialSlot, useModNormal, normalSlot, layout, null));
		}
		string text3 = StringRewriter.RetargetFileName(Path.GetFileName(text2), plane, sourceSlot, destSlot);
		AcePackage.WritePlain(Path.Combine(text, text3), item, array);
		log?.Invoke("Material " + Path.GetFileName(text2) + " -> " + text3);
	}

	public static void StageStockDecal(string pawnRoot, string stageRoot, string plane, string sourceSlot, string destSlot, Action<string>? log, bool replaceExisting = false, bool useModMaterial = true, string? materialSlot = null, bool useModNormal = false, string? normalSlot = null, SlotTextureLayout layout = SlotTextureLayout.Standard, string? sharedBody = null)
	{
		string text = Path.Combine(stageRoot, "Nimbus", "Content", "Vehicles", "Aircraft", plane, destSlot);
		if (!replaceExisting && Directory.Exists(text) && Directory.EnumerateFiles(text, "*.uasset").Any((string file) => Path.GetFileName(file).Contains("Decal", StringComparison.OrdinalIgnoreCase)))
		{
			return;
		}
		string text2 = PawnLibrary.StockDecal(pawnRoot, plane, sourceSlot);
		string? bodyText = null;
		if (layout == SlotTextureLayout.Shared && !string.IsNullOrWhiteSpace(sharedBody))
		{
			bodyText = Encoding.ASCII.GetString(AcePackage.ReadPlain(sharedBody).Asset);
		}
		(byte[] Asset, byte[]? Export) tuple = AcePackage.ReadPlain(text2);
		byte[] item = tuple.Asset;
		byte[] array = tuple.Export;
		item = StringRewriter.Rewrite(item, (string text4) => MapCopiedMaterial(text4, plane, sourceSlot, destSlot, useModMaterial, materialSlot, useModNormal, normalSlot, layout, bodyText));
		if (array != null)
		{
			array = StringRewriter.Rewrite(array, (string text4) => MapCopiedMaterial(text4, plane, sourceSlot, destSlot, useModMaterial, materialSlot, useModNormal, normalSlot, layout, bodyText));
		}
		string text3 = StringRewriter.RetargetFileName(Path.GetFileName(text2), plane, sourceSlot, destSlot);
		AcePackage.WritePlain(Path.Combine(text, text3), item, array);
		log?.Invoke("Decal " + Path.GetFileName(text2) + " -> " + text3);
	}

	private static string? MapCopiedMaterial(string text, string plane, string instanceSlot, string destSlot, bool useModMaterial, string? materialSlot, bool useModNormal, string? normalSlot, SlotTextureLayout layout, string? bodyText)
	{
		if (layout == SlotTextureLayout.Shared)
		{
			string current = text;
			if (bodyText != null)
			{
				string? followed = FollowBodyTextures(current, bodyText);
				if (followed != null)
				{
					current = followed;
				}
			}
			string? identity = RetargetIdentity(current, plane, instanceSlot, destSlot);
			if (identity != null)
			{
				current = identity;
			}
			return FinishDam(current, text, plane, instanceSlot, destSlot);
		}
		if (layout == SlotTextureLayout.BulkOnly)
		{
			if (IsNormalReference(text, plane))
			{
				return FinishDam(PointNormal(text, plane, string.IsNullOrWhiteSpace(normalSlot) ? instanceSlot : normalSlot) ?? text, text, plane, instanceSlot, destSlot);
			}
			if (IsMrecReference(text, plane))
			{
				return FinishDam(PointMrec(text, plane, string.IsNullOrWhiteSpace(materialSlot) ? instanceSlot : materialSlot) ?? text, text, plane, instanceSlot, destSlot);
			}
			if (IsDiffuseReference(text, plane))
			{
				return FinishDam(PointDiffuse(text, plane, destSlot) ?? text, text, plane, instanceSlot, destSlot);
			}
			return FinishDam(RetargetIdentity(text, plane, instanceSlot, destSlot) ?? text, text, plane, instanceSlot, destSlot);
		}
		string? standard;
		if (IsNormalReference(text, plane))
		{
			standard = PointNormal(text, plane, destSlot);
		}
		else if (IsMrecReference(text, plane))
		{
			standard = PointMrec(text, plane, destSlot);
		}
		else if (IsDiffuseReference(text, plane))
		{
			string diffuseSlot = useModMaterial ? destSlot : (string.IsNullOrWhiteSpace(materialSlot) ? instanceSlot : materialSlot);
			standard = PointDiffuse(text, plane, diffuseSlot);
		}
		else
		{
			standard = StringRewriter.RetargetMaterialText(text, plane, instanceSlot, destSlot);
		}
		return FinishDam(standard ?? text, text, plane, instanceSlot, destSlot);
	}

	public static void StageSavedTexture(string pawnRoot, string stageRoot, string plane, string sourceSlot, string destSlot, string suffix, Action<string>? log = null, bool copyBulk = true)
	{
		string source = PawnLibrary.StockTexture(pawnRoot, plane, sourceSlot, suffix);
		string directory = Path.Combine(stageRoot, "Nimbus", "Content", "Vehicles", "Aircraft", plane, destSlot);
		string destName = plane + "_" + destSlot + "_" + suffix + ".uasset";
		if (Directory.Exists(directory) && File.Exists(Path.Combine(directory, destName)))
		{
			return;
		}
		string destUasset = Path.Combine(directory, destName);
		AcePackage.WriteRetargetedTexture(source, destUasset, plane, sourceSlot, destSlot);
		if (copyBulk)
		{
			string bulk = Path.ChangeExtension(source, ".ubulk");
			if (File.Exists(bulk))
			{
				File.Copy(bulk, Path.ChangeExtension(destUasset, ".ubulk"), overwrite: true);
			}
		}
		log?.Invoke(suffix + " " + Path.GetFileName(source) + " -> " + destName);
	}

	private static bool IsNormalReference(string text, string plane)
	{
		return Regex.IsMatch(text, Regex.Escape(plane) + "_\\d{2}_N\\b", RegexOptions.IgnoreCase);
	}

	private static bool IsMrecReference(string text, string plane)
	{
		return Regex.IsMatch(text, Regex.Escape(plane) + "_(?:x\\d+_MREC|\\d{2}_MREC|\\d{2}xMREC)\\b", RegexOptions.IgnoreCase);
	}

	private static bool IsDiffuseReference(string text, string plane)
	{
		return Regex.IsMatch(text, Regex.Escape(plane) + "_\\d{2}_D\\b", RegexOptions.IgnoreCase);
	}

	private static string? PointMrec(string text, string plane, string destSlot)
	{
		Match match = Regex.Match(text, Regex.Escape(plane) + "_(\\d{2})(?:_MREC|xMREC)\\b", RegexOptions.IgnoreCase);
		if (match.Success)
		{
			return StringRewriter.RetargetMaterialText(text, plane, match.Groups[1].Value, destSlot);
		}
		if (Regex.IsMatch(text, Regex.Escape(plane) + "_x\\d+_MREC\\b", RegexOptions.IgnoreCase))
		{
			return StringRewriter.RetargetMaterialText(text, plane, "00", destSlot);
		}
		return null;
	}

	private static string? PointDiffuse(string text, string plane, string diffuseSlot)
	{
		Match match = Regex.Match(text, Regex.Escape(plane) + "_(\\d{2})_D\\b", RegexOptions.IgnoreCase);
		if (!match.Success)
		{
			return null;
		}
		return StringRewriter.RetargetMaterialText(text, plane, match.Groups[1].Value, diffuseSlot);
	}

	private static string? PointNormal(string text, string plane, string normalSlot)
	{
		Match match = Regex.Match(text, Regex.Escape(plane) + "_(\\d{2})_N\\b", RegexOptions.IgnoreCase);
		if (!match.Success)
		{
			return null;
		}
		string current = match.Groups[1].Value;
		if (current.Equals(normalSlot, StringComparison.OrdinalIgnoreCase))
		{
			return null;
		}
		string updated = Regex.Replace(text, $"(Aircraft[\\\\/]{Regex.Escape(plane)}[\\\\/]){current}([\\\\/]|$)", "${1}" + normalSlot + "$2", RegexOptions.IgnoreCase);
		updated = Regex.Replace(updated, Regex.Escape(plane) + "_" + current + "_N\\b", plane + "_" + normalSlot + "_N", RegexOptions.IgnoreCase);
		return updated == text ? null : updated;
	}

	public static void StageLooseBulk(string sourceRoot, string stageRoot, string plane, string sourceSlot, string destSlot, Action<string>? log = null)
	{
		foreach (string file in Directory.GetFiles(sourceRoot, "*.ubulk", SearchOption.AllDirectories))
		{
			string relative = Path.GetRelativePath(sourceRoot, file).Replace('\\', '/');
			string directory = Path.GetDirectoryName(relative)?.Replace('\\', '/') ?? "";
			string name = Path.GetFileName(file);
			bool diffuse = name.EndsWith("_D.ubulk", StringComparison.OrdinalIgnoreCase);
			bool roughness = name.Contains("MREC", StringComparison.OrdinalIgnoreCase);
			if (diffuse || roughness)
			{
				directory = RetargetDirectory(directory, plane, sourceSlot, destSlot);
				name = StringRewriter.RetargetFileName(name, plane, sourceSlot, destSlot);
			}
			string dest = Path.Combine(stageRoot, directory.Replace('/', Path.DirectorySeparatorChar), name);
			Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
			File.Copy(file, dest, overwrite: true);
			log?.Invoke((diffuse ? "Diffuse bulk " : "Roughness bulk ") + Path.GetFileName(file) + " -> " + name);
		}
	}

	private static SlotTextureLayout ClassifyLayout(IEnumerable<string> files, string plane, string? instanceUasset)
	{
		bool uasset = false;
		bool ubulk = false;
		foreach (string file in files)
		{
			if (file.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase))
			{
				uasset = true;
			}
			else if (file.EndsWith(".ubulk", StringComparison.OrdinalIgnoreCase))
			{
				ubulk = true;
			}
		}
		if (ubulk && !uasset)
		{
			return SlotTextureLayout.BulkOnly;
		}
		if (instanceUasset != null && !ShipsTextureFile(files))
		{
			return SlotTextureLayout.Shared;
		}
		if (instanceUasset != null && Regex.IsMatch(Encoding.ASCII.GetString(AcePackage.ReadPlain(instanceUasset).Asset), "Aircraft/" + Regex.Escape(plane) + "/(?!\\d{2}(?:/|$))[^/\\s]+/", RegexOptions.IgnoreCase))
		{
			return SlotTextureLayout.Shared;
		}
		return SlotTextureLayout.Standard;
	}

	private static bool ShipsTextureFile(IEnumerable<string> files)
	{
		return HasPayload(files, (string name) => name.Contains("MREC", StringComparison.OrdinalIgnoreCase) || name.EndsWith("_D", StringComparison.OrdinalIgnoreCase) || name.EndsWith("_N", StringComparison.OrdinalIgnoreCase));
	}

	private static bool HasPayload(IEnumerable<string> files, Func<string, bool> match, string? extensionFilter = null)
	{
		foreach (string file in files)
		{
			string extension = Path.GetExtension(file);
			if (extensionFilter != null)
			{
				if (!extension.Equals(extensionFilter, StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}
			}
			else if (!extension.Equals(".uasset", StringComparison.OrdinalIgnoreCase) && !extension.Equals(".uexp", StringComparison.OrdinalIgnoreCase) && !extension.Equals(".ubulk", StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}
			string name = Path.GetFileNameWithoutExtension(file);
			if (name.StartsWith("AcePlayerPawn_", StringComparison.OrdinalIgnoreCase) || name.Contains("Inst", StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}
			if (match(name))
			{
				return true;
			}
		}
		return false;
	}

	private static void ReadSharedTargets(string text, string plane, out bool materialFromShared, out string? materialSlot, out bool normalFromShared, out string? normalSlot)
	{
		text = string.Join("\n", AssetNames(text));
		normalSlot = ReferencedSlot(text, plane, "_N");
		normalFromShared = HasCustomTexture(text, plane, "_N") || normalSlot == null;
		if (HasCustomTexture(text, plane, "MREC"))
		{
			materialFromShared = true;
			materialSlot = null;
			return;
		}
		string? mrecSlot = ReferencedSlot(text, plane, "_MREC");
		if (mrecSlot != null)
		{
			materialFromShared = false;
			materialSlot = mrecSlot;
			return;
		}
		string? diffuseSlot = ReferencedSlot(text, plane, "_D");
		if (HasCustomTexture(text, plane, "_D") || HasCustomFolder(text, plane) || diffuseSlot == null)
		{
			materialFromShared = true;
			materialSlot = null;
			return;
		}
		materialFromShared = false;
		materialSlot = diffuseSlot;
	}

	private static List<string> AssetNames(string text)
	{
		List<string> names = new List<string>();
		int index = 0;
		while (index < text.Length)
		{
			if (text[index] < 32 || text[index] > 126)
			{
				index++;
				continue;
			}
			int start = index;
			while (index < text.Length && text[index] >= 32 && text[index] <= 126)
			{
				index++;
			}
			int length = index - start;
			if (length < 4 || length > 180)
			{
				continue;
			}
			string value = text.Substring(start, length);
			if (value.StartsWith("/Game/", StringComparison.OrdinalIgnoreCase) || value.Contains("Aircraft/", StringComparison.OrdinalIgnoreCase) || Regex.IsMatch(value, "^[A-Za-z0-9_\\-]+$"))
			{
				names.Add(value);
			}
		}
		return names;
	}

	private static bool HasCustomFolder(string text, string plane)
	{
		return Regex.IsMatch(text, "Aircraft/" + Regex.Escape(plane) + "/(?!\\d{2}(?:/|$))[^/\\s]+/", RegexOptions.IgnoreCase);
	}

	private static bool HasCustomTexture(string text, string plane, string marker)
	{
		if (Regex.IsMatch(text, "Aircraft/" + Regex.Escape(plane) + "/(?!\\d{2}(?:/|$))[^/\\s]+/[^\\s]*" + Regex.Escape(marker), RegexOptions.IgnoreCase))
		{
			return true;
		}
		foreach (Match match in Regex.Matches(text, Regex.Escape(plane) + "_([A-Za-z0-9]+)_?" + Regex.Escape(marker) + "\\b", RegexOptions.IgnoreCase))
		{
			string token = match.Groups[1].Value;
			if (token.Length != 2 || !token.All(char.IsDigit))
			{
				return true;
			}
		}
		return false;
	}

	private static string? RetargetIdentity(string text, string plane, string sourceSlot, string destSlot)
	{
		if (text.Contains(plane + "_" + sourceSlot + "_CP_Inst", StringComparison.OrdinalIgnoreCase))
		{
			return null;
		}
		string updated = text;
		if (!updated.Contains(plane + "_00_N", StringComparison.OrdinalIgnoreCase))
		{
			updated = Regex.Replace(updated, "(Aircraft[\\\\/]" + Regex.Escape(plane) + "[\\\\/])" + sourceSlot + "([\\\\/]|$)", "${1}" + destSlot + "$2", RegexOptions.IgnoreCase);
		}
		updated = Regex.Replace(updated, Regex.Escape(plane + "_" + sourceSlot + "_Dam_Inst"), plane + "_" + destSlot + "_Dam_Inst", RegexOptions.IgnoreCase);
		updated = Regex.Replace(updated, Regex.Escape(plane + "_" + sourceSlot + "_Decal_Inst"), plane + "_" + destSlot + "_Decal_Inst", RegexOptions.IgnoreCase);
		updated = Regex.Replace(updated, Regex.Escape(plane + "_" + sourceSlot + "_Inst"), plane + "_" + destSlot + "_Inst", RegexOptions.IgnoreCase);
		return updated == text ? null : updated;
	}

	private static string? FinishDam(string current, string original, string plane, string sourceSlot, string destSlot)
	{
		string from = plane + "_" + sourceSlot + "_Dam_Inst";
		if (current.IndexOf(from, StringComparison.OrdinalIgnoreCase) < 0)
		{
			return current == original ? null : current;
		}
		string updated = Regex.Replace(current, Regex.Escape(from), plane + "_" + destSlot + "_Dam_Inst", RegexOptions.IgnoreCase);
		return updated == original ? null : updated;
	}

	private static string? FollowBodyTextures(string text, string bodyText)
	{
		string updated = ReplaceTextureRole(text, bodyText, "_D");
		updated = ReplaceTextureRole(updated, bodyText, "_MREC");
		updated = ReplaceTextureRole(updated, bodyText, "_N");
		return updated == text ? null : updated;
	}

	private static string ReplaceTextureRole(string text, string bodyText, string suffix)
	{
		List<string> bodyNames = ReadableStrings(bodyText).Where((string value) => IsTextureSuffix(value, suffix)).ToList();
		List<string> sourceNames = ReadableStrings(text).Where((string value) => IsTextureSuffix(value, suffix)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
		string updated = text;
		foreach (string source in sourceNames)
		{
			string? target = ChooseTextureName(bodyNames, source.Length);
			if (target == null || target.Equals(source, StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}
			updated = Regex.Replace(updated, Regex.Escape(source), target, RegexOptions.IgnoreCase);
		}
		return updated;
	}

	private static bool IsTextureSuffix(string text, string suffix)
	{
		if (!text.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) || text.Contains("_Inst", StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}
		if (suffix.Equals("_N", StringComparison.OrdinalIgnoreCase) && text.Contains("Dam", StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}
		return true;
	}

	private static string? ChooseTextureName(List<string> names, int length)
	{
		List<string> same = new List<string>();
		foreach (string name in names)
		{
			if (name.Length == length && !same.Exists((string value) => value.Equals(name, StringComparison.OrdinalIgnoreCase)))
			{
				same.Add(name);
			}
		}
		if (same.Count == 0)
		{
			return null;
		}
		foreach (string name in same)
		{
			bool insideLonger = false;
			foreach (string other in names)
			{
				if (other.Length > name.Length && other.Contains(name, StringComparison.Ordinal))
				{
					insideLonger = true;
					break;
				}
			}
			if (!insideLonger)
			{
				return name;
			}
		}
		return same[0];
	}

	private static List<string> ReadableStrings(string text)
	{
		List<string> list = new List<string>();
		foreach (Match match in Regex.Matches(text, "[\\x20-\\x7e]{4,}"))
		{
			list.Add(match.Value);
		}
		return list;
	}

	private static string? ReferencedSlot(string text, string plane, string suffix)
	{
		Match match = Regex.Match(text, Regex.Escape(plane) + "_(\\d{2})" + suffix + "\\b", RegexOptions.IgnoreCase);
		return match.Success ? match.Groups[1].Value : null;
	}

	private static bool UseModTexture(string? choice, bool shipped)
	{
		if (string.Equals(choice, "mod", StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}
		if (string.IsNullOrWhiteSpace(choice))
		{
			return shipped;
		}
		return false;
	}

	public static void StageReplacer(string sourceRoot, string stageRoot, string plane, string sourceSlot, string destSlot, bool useModMaterial, bool useModNormal, string? materialSlot, string? normalSlot, SlotTextureLayout layout, Action<string>? log)
	{
		string[] files = Directory.GetFiles(sourceRoot, "*.*", SearchOption.AllDirectories);
		foreach (string text in files)
		{
			string extension = Path.GetExtension(text);
			if (extension.Equals(".uexp", StringComparison.OrdinalIgnoreCase) || extension.Equals(".ubulk", StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}
			if (Path.GetFileName(text).StartsWith("AcePlayerPawn_", StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}
			if (layout == SlotTextureLayout.Standard && !useModMaterial && PawnLibrary.IsMaterialTexture(Path.GetFileName(text), plane))
			{
				continue;
			}
			if (layout == SlotTextureLayout.Standard && !useModNormal && PawnLibrary.IsNormalTexture(Path.GetFileName(text), plane))
			{
				continue;
			}
			string text2 = RetargetDirectory(Path.GetDirectoryName(Path.GetRelativePath(sourceRoot, text).Replace('\\', '/'))?.Replace('\\', '/') ?? "", plane, sourceSlot, destSlot);
			string text3 = StringRewriter.RetargetFileName(Path.GetFileName(text), plane, sourceSlot, destSlot);
			string text4 = Path.Combine(stageRoot, text2.Replace('/', Path.DirectorySeparatorChar), text3);
			if (extension.Equals(".uasset", StringComparison.OrdinalIgnoreCase))
			{
				(byte[] Asset, byte[]? Export) tuple = AcePackage.ReadPlain(text);
				byte[] item = tuple.Asset;
				byte[] array = tuple.Export;
				item = StringRewriter.Rewrite(item, (string text6) => MapCopiedMaterial(text6, plane, sourceSlot, destSlot, useModMaterial, materialSlot, useModNormal, normalSlot, layout, null));
				if (array != null)
				{
					array = StringRewriter.Rewrite(array, (string text6) => MapCopiedMaterial(text6, plane, sourceSlot, destSlot, useModMaterial, materialSlot, useModNormal, normalSlot, layout, null));
				}
				AcePackage.WritePlain(text4, item, array);
				string text5 = Path.ChangeExtension(text, ".ubulk");
				if (File.Exists(text5))
				{
					string path = StringRewriter.RetargetFileName(Path.GetFileName(text5), plane, sourceSlot, destSlot);
					string destFileName = Path.Combine(Path.GetDirectoryName(text4), path);
					File.Copy(text5, destFileName, overwrite: true);
				}
				log?.Invoke("Retargeted " + Path.GetFileName(text) + " -> " + text3);
			}
			else
			{
				Directory.CreateDirectory(Path.GetDirectoryName(text4));
				File.Copy(text, text4, overwrite: true);
			}
		}
	}

	public static string PakFileName(string planeLabel, string displayName, int hangarSlot)
	{
		string text = planeLabel.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? planeLabel;
		return $"SkinReSlotter_{FileToken(text)}_{FileToken(displayName)}_Skin{hangarSlot}_P.pak";
	}

	public static string FileToken(string text)
	{
		string text2 = new string(text.Select(delegate(char character)
		{
			if (character == '/')
			{
				return '-';
			}
			return (!char.IsLetterOrDigit(character) && character != '-') ? '_' : character;
		}).ToArray());
		while (text2.Contains("__", StringComparison.Ordinal))
		{
			text2 = text2.Replace("__", "_", StringComparison.Ordinal);
		}
		text2 = text2.Trim('_');
		if (text2.Length != 0)
		{
			return text2;
		}
		return "Skin";
	}

	private static int? HangarToSuffix(int? hangarSlot)
	{
		if (hangarSlot.HasValue)
		{
			int valueOrDefault = hangarSlot.GetValueOrDefault();
			if (valueOrDefault < 2)
			{
				throw new InvalidOperationException($"Slot {valueOrDefault} is the first skin. New slots start at 2.");
			}
			return valueOrDefault - 1;
		}
		return null;
	}

	private static string ContentDirectory(SkinSlotBuildRequest request)
	{
		if (!string.IsNullOrWhiteSpace(request.ModsDirectory) && Directory.Exists(request.ModsDirectory))
		{
			return request.ModsDirectory;
		}
		if (!string.IsNullOrWhiteSpace(request.OutputDirectory) && Directory.Exists(request.OutputDirectory))
		{
			return request.OutputDirectory;
		}
		return Path.GetDirectoryName(File.Exists(request.SourcePath) ? request.SourcePath : request.SourcePath.TrimEnd('\\', '/')) ?? ".";
	}

	private static string DataDirectory(SkinSlotBuildRequest request, string contentPak)
	{
		if (!string.IsNullOrWhiteSpace(request.ModsDirectory) && Directory.Exists(request.ModsDirectory))
		{
			return request.ModsDirectory;
		}
		return Path.GetDirectoryName(contentPak) ?? ".";
	}

	private static string? PaksDirectory(string? outputDirectory)
	{
		if (string.IsNullOrWhiteSpace(outputDirectory) || !Directory.Exists(outputDirectory))
		{
			return null;
		}
		string fullPath = Path.GetFullPath(outputDirectory);
		if (Path.GetFileName(fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)).Equals("~mods", StringComparison.OrdinalIgnoreCase))
		{
			string directoryName = Path.GetDirectoryName(fullPath);
			if (!string.IsNullOrEmpty(directoryName) && Directory.Exists(directoryName))
			{
				return directoryName;
			}
		}
		return fullPath;
	}

	private static string ChosenSlot(string? chosen, string fallback)
	{
		if (string.IsNullOrWhiteSpace(chosen) || chosen.Trim().Equals("shared", StringComparison.OrdinalIgnoreCase))
		{
			return fallback;
		}
		string text = chosen.Trim();
		if (text.Length == 2 && text.All(char.IsDigit))
		{
			return text;
		}
		throw new InvalidOperationException("Material slot '" + text + "' has to be a two-digit folder such as 02.");
	}

	public static string RetargetDirectory(string relativeDir, string plane, string sourceSlot, string destSlot)
	{
		return Regex.Replace(relativeDir, $"(Aircraft/{Regex.Escape(plane)}/){sourceSlot}(?=/|$)", "${1}" + destSlot, RegexOptions.IgnoreCase);
	}

	private static string ReplaceOrdinal(string text, string oldValue, string newValue)
	{
		int num = text.IndexOf(oldValue, StringComparison.OrdinalIgnoreCase);
		if (num < 0)
		{
			return text;
		}
		string text2 = text.Substring(0, num);
		int num2 = num + oldValue.Length;
		return text2 + newValue + text.Substring(num2, text.Length - num2);
	}

	private static void TryDelete(string directory)
	{
		try
		{
			if (Directory.Exists(directory))
			{
				Directory.Delete(directory, recursive: true);
			}
		}
		catch
		{
		}
	}
}
