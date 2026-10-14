package studcraft.mixin;

import com.mojang.blaze3d.vertex.PoseStack;
import net.minecraft.client.Camera;
import net.minecraft.client.renderer.LevelRenderer;
import org.joml.Matrix4f;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.Inject;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfo;
import studcraft.RobloxSky;

/** hooks.sky_box: Roblox's skybox over the vanilla dome, under the sun, moon and stars. */
@Mixin(LevelRenderer.class)
public abstract class LevelRendererMixin {
	@Inject(
		method = "renderSky",
		at = @At(value = "INVOKE", target = "Lcom/mojang/blaze3d/vertex/VertexBuffer;unbind()V", ordinal = 0, shift = At.Shift.AFTER)
	)
	private void studcraft$robloxSky(Matrix4f view, Matrix4f projection, float partialTick, Camera camera, boolean foggy, Runnable fogSetup, CallbackInfo ci) {
		RobloxSky.render(view, partialTick);
	}

	/** hooks.no_clouds: Roblox's sky has its own clouds. */
	@Inject(method = "renderClouds", at = @At("HEAD"), cancellable = true)
	private void studcraft$noClouds(PoseStack poseStack, Matrix4f view, Matrix4f projection, float partialTick, double x, double y, double z, CallbackInfo ci) {
		if (RobloxSky.active()) ci.cancel();
	}
}
