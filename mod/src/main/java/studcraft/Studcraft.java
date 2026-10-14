package studcraft;

import net.fabricmc.api.ClientModInitializer;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

public final class Studcraft implements ClientModInitializer {
	public static final Logger LOG = LoggerFactory.getLogger("studcraft");

	@Override
	public void onInitializeClient() {
		RobloxInstall.versionDir();
		MeltyTest.start();
	}
}
