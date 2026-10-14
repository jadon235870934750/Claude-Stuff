package robloxcraft;

import net.fabricmc.api.ClientModInitializer;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

public final class Robloxcraft implements ClientModInitializer {
	public static final Logger LOG = LoggerFactory.getLogger("robloxcraft");

	@Override
	public void onInitializeClient() {
		RobloxInstall.versionDir();
	}
}
