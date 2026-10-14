package studcraft;

import com.google.gson.JsonElement;
import com.google.gson.JsonObject;
import com.google.gson.JsonParser;
import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import net.minecraft.client.Minecraft;
import net.minecraft.client.gui.screens.TitleScreen;
import net.minecraft.world.Difficulty;
import net.minecraft.world.level.GameRules;
import net.minecraft.world.level.GameType;
import net.minecraft.world.level.LevelSettings;
import net.minecraft.world.level.WorldDataConfiguration;
import net.minecraft.world.level.levelgen.WorldOptions;
import net.minecraft.world.level.levelgen.presets.WorldPresets;
import studcraft.gen.Sheets;

/**
 * Melty's test mode. Inert unless Melty's test file exists. When it does, the mod makes a fresh world
 * from the test seed, shows the Roblox look at noon, and reports the test_checks sheet's checks.
 */
public final class TestMode {
	private static final String SLUG = "studcraft";
	private static final int SETTLE_TICKS = 200;

	private static boolean read;
	private static JsonObject test;
	private static boolean worldStarted;
	private static int ticksInWorld;
	private static boolean reported;

	private TestMode() {}

	private static Path testFile() {
		String local = System.getenv("LOCALAPPDATA");
		return local == null ? null : Path.of(local, "Melty", "test", SLUG + ".json");
	}

	private static boolean active() {
		if (!read) {
			read = true;
			Path file = testFile();
			if (file != null && Files.isRegularFile(file)) {
				try {
					test = JsonParser.parseString(Files.readString(file, StandardCharsets.UTF_8)).getAsJsonObject();
					Studcraft.LOG.info("Studcraft: Melty test mode, run {} of {}", str("run"), str("runs"));
				} catch (IOException | RuntimeException e) {
					Studcraft.LOG.warn("Studcraft: unreadable Melty test file {}", file, e);
				}
			}
		}
		return test != null;
	}

	/** Called at the start of every client tick (MinecraftMixin). */
	public static void tick(Minecraft mc) {
		if (reported || !active()) return;
		if (!worldStarted) {
			if (mc.level == null && mc.screen instanceof TitleScreen) startWorld(mc);
			return;
		}
		if (mc.level == null || mc.player == null) return;
		ticksInWorld++;
		if (ticksInWorld == 40) {
			mc.player.connection.sendCommand("time set 1000");
			mc.player.connection.sendCommand("weather clear");
			mc.player.setXRot(-8.0F);
		}
		if (ticksInWorld >= SETTLE_TICKS) report();
	}

	private static void startWorld(Minecraft mc) {
		worldStarted = true;
		String name = "Studcraft Test " + str("testId").replaceAll("[^A-Za-z0-9-]", "") + " " + str("run");
		long seed = seed();
		LevelSettings settings = new LevelSettings(
			name, GameType.CREATIVE, false, Difficulty.PEACEFUL, true, new GameRules(), WorldDataConfiguration.DEFAULT);
		Studcraft.LOG.info("Studcraft: test world '{}' seed {}", name, seed);
		mc.createWorldOpenFlows().createFreshLevel(
			name, settings, new WorldOptions(seed, true, false), WorldPresets::createNormalWorldDimensions, mc.screen);
	}

	private static void report() {
		reported = true;
		for (String check : Sheets.TEST_CHECKS) {
			String why = failure(check);
			MeltyTest.report(check, why == null, why);
			Studcraft.LOG.info("Studcraft: test check {} {}", check, why == null ? "pass" : "fail: " + why);
		}
	}

	/** Null when the check passes, else why it failed. */
	private static String failure(String check) {
		boolean roblox = RobloxInstall.present();
		return switch (check) {
			case "roblox-found" -> roblox ? null : "no Roblox install found (set " + RobloxInstall.ENV + ")";
			case "studs-applied" -> RobloxPack.styledCount() > 0 ? null : "no block texture was restyled";
			case "roblox-sky" -> RobloxSky.framesDrawn() > 0 ? null : "the Roblox skybox was never drawn";
			case "roblox-font" -> RobloxPack.served("font_file") ? null : "Minecraft never opened Roblox's font";
			case "roblox-oof" -> RobloxPack.available("hurt1") ? null : "Roblox's oof.ogg could not be read";
			default -> "unknown check";
		};
	}

	private static long seed() {
		JsonElement e = test.get("seed");
		if (e == null || e.isJsonNull()) return 20261014L;
		try {
			return e.getAsLong();
		} catch (RuntimeException notANumber) {
			return e.getAsString().hashCode();
		}
	}

	private static String str(String key) {
		JsonElement e = test.get(key);
		return e == null || e.isJsonNull() ? "" : e.getAsString();
	}
}
