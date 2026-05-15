extends SceneTree


const Const = preload("res://src/Const.gd")
const Helpers = preload("res://src/Helpers.gd")
const RESULT_OK := 0
const RESULT_ERROR := 1

var request_path := ""
var response_path := ""
var request := {}
var helpers := Helpers.new()
var response := {
	"ok": false,
	"command": "",
	"outputs": {},
	"warnings": [],
	"errors": [],
	"metadata": {}
}


func _init():
	_run()


func _run():
	if not _parse_args():
		_finish(RESULT_ERROR)
		return

	if not _load_request():
		_finish(RESULT_ERROR)
		return

	var command := str(request.get("command", ""))
	response["command"] = command

	match command:
		"create_project_from_art":
			create_project_from_art()
		"create_tile":
			create_tile()
		"inspect_project":
			inspect_project()
		"list_rulesets":
			list_rulesets()
		"list_templates":
			list_templates()
		"validate_ruleset":
			validate_ruleset()
		"validate_template":
			validate_template()
		"validate_tile":
			validate_tile(true)
		"render_tile", "export_texture":
			var render_state = render_tile()
			if render_state is GDScriptFunctionState:
				yield(render_state, "completed")
		"export_subtiles":
			var subtile_state = export_subtiles()
			if subtile_state is GDScriptFunctionState:
				yield(subtile_state, "completed")
		"export_mask_set":
			var mask_state = export_subtiles()
			if mask_state is GDScriptFunctionState:
				yield(mask_state, "completed")
		"export_unity_rule_tile":
			var unity_state = export_unity_rule_tile()
			if unity_state is GDScriptFunctionState:
				yield(unity_state, "completed")
		_:
			_add_error("Unknown command: %s" % command)

	_finish(RESULT_OK if response["ok"] else RESULT_ERROR)


func create_project_from_art():
	var project_dir := _get_or_create_project_dir()
	if project_dir.empty():
		return
	create_tile()


func create_tile():
	var project_dir := _get_or_create_project_dir()
	if project_dir.empty():
		return

	var tile_file := str(request.get("tile_file", ""))
	if tile_file.empty():
		_add_error("Missing tile_file.")
		return
	if not tile_file.ends_with("." + Const.TILE_EXTENXSION):
		tile_file += "." + Const.TILE_EXTENXSION

	var source_png := _get_required_path("source_png")
	var ruleset_path := _get_required_path("ruleset_path")
	var template_path := _get_required_path("template_path")
	if not response["errors"].empty():
		return

	_ensure_dir(project_dir + Const.TEXTURE_DIR)
	_ensure_dir(project_dir + Const.RULESET_DIR)
	_ensure_dir(project_dir + Const.TEMPLATE_DIR)

	var texture_rel := Const.TEXTURE_DIR + _safe_file_name(str(request.get("texture_name", source_png.get_file())))
	var ruleset_rel := Const.RULESET_DIR + _safe_file_name(str(request.get("ruleset_name", ruleset_path.get_file())))
	var template_rel := Const.TEMPLATE_DIR + _safe_file_name(str(request.get("template_name", template_path.get_file())))

	if not _copy_png(source_png, project_dir + texture_rel):
		return
	if not _copy_text_file(ruleset_path, project_dir + ruleset_rel):
		return
	if not _copy_png(template_path, project_dir + template_rel):
		return

	var input_size := _request_vector("input_tile_size", Const.DEFAULT_TILE_SIZE)
	var output_size := _request_vector("output_tile_size", input_size)
	var subtile_spacing := _request_vector("subtile_spacing", Vector2.ZERO)
	var output_resize := bool(request.get("output_resize", output_size != input_size))
	var tile_data := {
		"texture": texture_rel,
		"ruleset": ruleset_rel,
		"template": template_rel,
		"output_tile_size": _vector_dict(output_size),
		"random_seed_enabled": bool(request.get("random_seed_enabled", false)),
		"smoothing": bool(request.get("smoothing", false)),
		"merge_level": _vector_dict(_request_vector("merge_level", Vector2(0.25, 0.25))),
		"overlap_level": _vector_dict(_request_vector("overlap_level", Vector2(0.25, 0.25))),
		"input_tile_size": _vector_dict(input_size),
		"output_resize": output_resize,
		"subtile_spacing": _vector_dict(subtile_spacing),
		"ui_result_display_scale": float(request.get("ui_result_display_scale", 1.0))
	}

	var tile_path := project_dir + tile_file
	var file := File.new()
	if file.open(tile_path, File.WRITE) != OK:
		_add_error("Could not create tile file: %s" % tile_path)
		return
	file.store_string(JSON.print(tile_data, "\t"))
	file.close()

	response["outputs"]["tile_file"] = tile_path
	response["outputs"]["texture"] = project_dir + texture_rel
	response["outputs"]["ruleset"] = project_dir + ruleset_rel
	response["outputs"]["template"] = project_dir + template_rel
	response["metadata"] = {
		"project_dir": project_dir,
		"tile_file": tile_file,
		"tile_data": tile_data
	}
	response["ok"] = true


func _parse_args() -> bool:
	request_path = OS.get_environment("TILEPIPE_REQUEST")
	response_path = OS.get_environment("TILEPIPE_RESPONSE")
	var args := OS.get_cmdline_args()
	for i in range(args.size()):
		match args[i]:
			"--request":
				if i + 1 < args.size():
					request_path = args[i + 1]
			"--response":
				if i + 1 < args.size():
					response_path = args[i + 1]

	if request_path.empty():
		_add_error("Missing --request <path> argument.")
	if response_path.empty():
		_add_error("Missing --response <path> argument.")
	return response["errors"].empty()


func _load_request() -> bool:
	var file := File.new()
	if not file.file_exists(request_path):
		_add_error("Request file does not exist: %s" % request_path)
		return false

	if file.open(request_path, File.READ) != OK:
		_add_error("Could not open request file: %s" % request_path)
		return false

	var request_text := file.get_as_text()
	file.close()
	var parsed := JSON.parse(request_text)
	if parsed.error != OK or typeof(parsed.result) != TYPE_DICTIONARY:
		_add_error("Request JSON is invalid: %s" % parsed.error_string)
		return false

	request = parsed.result
	return true


func inspect_project():
	var project_dir := _get_project_dir()
	if project_dir.empty():
		return

	var result := {
		"project_dir": project_dir,
		"tiles": _scan_files(project_dir, Const.TILE_EXTENXSION, false),
		"rulesets": helpers.scan_for_rulesets_in_dir(project_dir + Const.RULESET_DIR),
		"templates": helpers.scan_for_templates_in_dir(project_dir + Const.TEMPLATE_DIR),
		"textures": helpers.scan_for_textures_in_dir(project_dir)
	}
	response["metadata"] = result
	response["ok"] = true


func list_rulesets():
	var project_dir := _get_project_dir()
	if project_dir.empty():
		return
	response["metadata"] = {
		"project_dir": project_dir,
		"rulesets": helpers.scan_for_rulesets_in_dir(project_dir + Const.RULESET_DIR)
	}
	response["ok"] = true


func list_templates():
	var project_dir := _get_project_dir()
	if project_dir.empty():
		return
	response["metadata"] = {
		"project_dir": project_dir,
		"templates": helpers.scan_for_templates_in_dir(project_dir + Const.TEMPLATE_DIR)
	}
	response["ok"] = true


func validate_ruleset():
	var path := _get_required_path("ruleset_path")
	if path.empty():
		return

	var ruleset := Ruleset.new(path)
	if ruleset.last_error != -1:
		_add_error(ruleset.last_error_message)
		return

	response["metadata"] = {
		"path": path,
		"name": ruleset.get_name(),
		"description": ruleset.get_description(),
		"parts": ruleset.parts,
		"rule_count": ruleset.get_subtiles().size()
	}
	response["ok"] = true


func validate_template():
	var path := _get_required_path("template_path")
	if path.empty():
		return

	var image := Image.new()
	var err := image.load(path)
	if err != OK:
		_add_error("Could not load template image: %s" % path)
		return

	if image.get_width() < Const.TEMPLATE_TILE_SIZE or image.get_height() < Const.TEMPLATE_TILE_SIZE:
		_add_error("Template image must be at least %dx%d." % [Const.TEMPLATE_TILE_SIZE, Const.TEMPLATE_TILE_SIZE])
		return

	if image.get_width() % Const.TEMPLATE_TILE_SIZE != 0 or image.get_height() % Const.TEMPLATE_TILE_SIZE != 0:
		_add_error("Template dimensions must be divisible by %d." % Const.TEMPLATE_TILE_SIZE)
		return

	response["metadata"] = _inspect_template_image(image)
	response["metadata"]["path"] = path
	response["ok"] = true


func validate_tile(mark_ok := true) -> TPTile:
	var tile := _load_tile_from_request()
	if tile == null:
		return null

	var errors := _collect_tile_errors(tile)
	for error in errors:
		_add_error(error)

	response["metadata"] = _metadata_for_tile(tile)
	if response["errors"].empty() and mark_ok:
		response["ok"] = true
	return tile


func render_tile():
	var tile := validate_tile(false)
	if tile == null or not response["errors"].empty():
		return

	yield(_render_all_frames(tile), "completed")
	if not _ensure_all_subtiles_rendered(tile):
		return

	var output_path := _get_output_path("output_path")
	if output_path.empty():
		return

	var export_image := tile.glue_frames_into_image()
	if export_image == null:
		_add_error("Tile render produced no output image.")
		return

	if export_image.save_png(output_path) != OK:
		_add_error("Could not save PNG output: %s" % output_path)
		return

	response["outputs"]["texture"] = output_path
	response["metadata"] = _metadata_for_tile(tile)
	response["ok"] = true


func export_unity_rule_tile():
	var tile := validate_tile(false)
	if tile == null or not response["errors"].empty():
		return

	yield(_render_all_frames(tile), "completed")
	if not _ensure_all_subtiles_rendered(tile):
		return

	var output_path := _get_output_path("output_path")
	var manifest_path := _get_output_path("manifest_path")
	if output_path.empty() or manifest_path.empty():
		return

	var export_image := tile.glue_frames_into_image()
	if export_image == null:
		_add_error("Tile render produced no output image.")
		return

	if export_image.save_png(output_path) != OK:
		_add_error("Could not save Unity atlas PNG: %s" % output_path)
		return

	var manifest := _unity_manifest_for_tile(tile, output_path)
	var file := File.new()
	if file.open(manifest_path, File.WRITE) != OK:
		_add_error("Could not write Unity manifest: %s" % manifest_path)
		return
	file.store_string(JSON.print(manifest, "\t"))
	file.close()

	response["outputs"]["texture"] = output_path
	response["outputs"]["manifest"] = manifest_path
	response["metadata"] = manifest
	response["ok"] = true


func export_subtiles():
	var tile := validate_tile(false)
	if tile == null or not response["errors"].empty():
		return

	yield(_render_all_frames(tile), "completed")
	if not _ensure_all_subtiles_rendered(tile):
		return

	var output_dir := _get_output_path("output_dir")
	if output_dir.empty():
		return

	var dir := Directory.new()
	if not dir.dir_exists(output_dir):
		_add_error("Output directory does not exist: %s" % output_dir)
		return

	var exported := []
	var requested_masks := _request_int_array("masks")
	var requested_frame := int(request.get("frame_index", -1))
	var tile_name := tile.tile_file_name.get_basename().get_file()
	for frame in tile.frames:
		if requested_frame >= 0 and frame.index != requested_frame:
			continue
		for bitmask in frame.result_subtiles_by_bitmask:
			if not requested_masks.empty() and not bitmask in requested_masks:
				continue
			var variant_index := 0
			for subtile in frame.result_subtiles_by_bitmask[bitmask]:
				var path := "%s/%s_frame_%d_mask_%d_variant_%d.png" % [
					output_dir,
					tile_name,
					frame.index,
					bitmask,
					variant_index
				]
				if subtile.image.save_png(path) != OK:
					_add_error("Could not save subtile PNG: %s" % path)
					return
				exported.append(path)
				variant_index += 1

	response["outputs"]["subtiles"] = exported
	response["metadata"] = _metadata_for_tile(tile)
	response["ok"] = true


func _render_all_frames(tile: TPTile):
	for frame_index in tile.frames.size():
		var renderer := TileRenderer.new()
		get_root().add_child(renderer)
		renderer.start_render(tile, frame_index)
		yield(renderer, "subtiles_ready")
		tile.frames[frame_index].merge_result_from_subtiles(
			tile.template_size,
			tile.get_output_tile_size(),
			tile.subtile_spacing
		)
		yield(self, "idle_frame")


func _load_tile_from_request() -> TPTile:
	var project_dir := _get_project_dir()
	var tile_file := str(request.get("tile_file", ""))
	if project_dir.empty():
		return null
	if tile_file.empty():
		_add_error("Missing tile_file.")
		return null

	var tile := TPTile.new()
	if tile == null:
		_add_error("Could not instantiate TPTile.")
		return null

	if not tile.load_tile(project_dir, tile_file):
		_add_error("Could not load tile file: %s%s" % [project_dir, tile_file])
		return null

	return tile


func _request_int_array(key: String) -> Array:
	var values := []
	if not request.has(key) or typeof(request[key]) != TYPE_ARRAY:
		return values
	for value in request[key]:
		values.append(int(value))
	return values


func _collect_tile_errors(tile: TPTile) -> Array:
	var errors := []
	if not tile.is_texture_loaded:
		errors.append("Tile texture failed to load.")
	if not tile.is_ruleset_loaded:
		errors.append("Tile ruleset failed to load.")
	if not tile.is_template_loaded:
		errors.append("Tile template failed to load.")
	if not tile.is_able_to_render():
		errors.append("Tile is not renderable.")
	return errors


func _ensure_all_subtiles_rendered(tile: TPTile) -> bool:
	for frame in tile.frames:
		for bitmask in frame.result_subtiles_by_bitmask:
			for subtile in frame.result_subtiles_by_bitmask[bitmask]:
				if subtile.image == null:
					_add_error("Missing rendered subtile for frame %d mask %d." % [frame.index, bitmask])
					return false
	return true


func _metadata_for_tile(tile: TPTile) -> Dictionary:
	var generated_masks := []
	var missing_rule_masks := []
	if tile.frames.size() > 0:
		for bitmask in tile.frames[0].result_subtiles_by_bitmask.keys():
			generated_masks.append(bitmask)
			if tile.ruleset.get_mask_data(bitmask).empty():
				missing_rule_masks.append(bitmask)
	generated_masks.sort()
	missing_rule_masks.sort()

	return {
		"project_dir": tile.current_directory,
		"tile_file": tile.tile_file_name,
		"texture_path": tile.texture_path,
		"ruleset_path": tile.ruleset_path,
		"template_path": tile.template_path,
		"input_tile_size": _vector_dict(tile.input_tile_size),
		"output_tile_size": _vector_dict(tile.get_output_tile_size()),
		"template_size": _vector_dict(tile.template_size),
		"frame_count": tile.frames.size(),
		"subtile_spacing": _vector_dict(tile.subtile_spacing),
		"generated_masks": generated_masks,
		"missing_rule_masks": missing_rule_masks,
		"rendered_size": _vector_dict(tile.get_full_tile_rendered_size())
	}


func _unity_manifest_for_tile(tile: TPTile, atlas_path: String) -> Dictionary:
	var frame_size := tile.get_rendered_frame_size()
	var tile_size := tile.get_output_tile_size()
	var sprite_rects := []
	var masks := {}
	for frame in tile.frames:
		var bitmasks: Array = frame.result_subtiles_by_bitmask.keys()
		bitmasks.sort()
		for bitmask in bitmasks:
			if not masks.has(str(bitmask)):
				masks[str(bitmask)] = []
			var variant_index := 0
			for subtile in frame.result_subtiles_by_bitmask[bitmask]:
				var rect_position: Vector2 = subtile.position_in_template * tile_size
				rect_position += subtile.position_in_template * tile.subtile_spacing
				rect_position.y += frame.index * frame_size.y
				var sprite_name := _unity_sprite_name(tile, frame.index, bitmask, variant_index)
				var sprite_data := {
					"name": sprite_name,
					"mask": bitmask,
					"frame_index": frame.index,
					"variant_index": variant_index,
					"template_position": _vector_dict(subtile.position_in_template),
					"rect": {
						"x": int(rect_position.x),
						"y": int(rect_position.y),
						"width": int(tile_size.x),
						"height": int(tile_size.y)
					}
				}
				sprite_rects.append(sprite_data)
				masks[str(bitmask)].append(sprite_name)
				variant_index += 1

	return {
		"format": "tilepipe2_unity_ruletile_manifest",
		"version": 1,
		"tile_name": tile.tile_file_name.get_basename().get_file(),
		"atlas_path": atlas_path,
		"project_dir": tile.current_directory,
		"tile_file": tile.tile_file_name,
		"source_files": {
			"tile": tile.current_directory + tile.tile_file_name,
			"texture": tile.texture_path,
			"ruleset": tile.ruleset_path,
			"template": tile.template_path
		},
		"tile_size": _vector_dict(tile_size),
		"spacing": _vector_dict(tile.subtile_spacing),
		"template_size": _vector_dict(tile.template_size),
		"frame_size": _vector_dict(frame_size),
		"frame_count": tile.frames.size(),
		"sprites": sprite_rects,
		"rules": masks
	}


func _unity_sprite_name(tile: TPTile, frame_index: int, bitmask: int, variant_index: int) -> String:
	return "%s_mask_%d_frame_%d_variant_%d" % [
		tile.tile_file_name.get_basename().get_file(),
		bitmask,
		frame_index,
		variant_index
	]


func _inspect_template_image(image: Image) -> Dictionary:
	var masks := []
	var positions := []
	var template_size := image.get_size() / Const.TEMPLATE_TILE_SIZE
	image.lock()
	for x in range(template_size.x):
		for y in range(template_size.y):
			if _template_has_tile(image, x, y):
				var mask := _template_mask_value(image, x, y)
				masks.append(mask)
				positions.append({"x": x, "y": y, "mask": mask})
	image.unlock()
	masks.sort()
	return {
		"size": _vector_dict(template_size),
		"tile_count": positions.size(),
		"masks": masks,
		"positions": positions
	}


func _template_mask_value(image: Image, x: int, y: int) -> int:
	var value := 0
	for mask in Const.TEMPLATE_MASK_CHECK_POINTS:
		var point: Vector2 = Const.TEMPLATE_MASK_CHECK_POINTS[mask]
		if not image.get_pixel(x * Const.TEMPLATE_TILE_SIZE + int(point.x), y * Const.TEMPLATE_TILE_SIZE + int(point.y)).is_equal_approx(Color.white):
			value += mask
	return value


func _template_has_tile(image: Image, x: int, y: int) -> bool:
	return not image.get_pixel(
		x * Const.TEMPLATE_TILE_SIZE + int(Const.MASK_CHECK_CENTER.x),
		y * Const.TEMPLATE_TILE_SIZE + int(Const.MASK_CHECK_CENTER.y)
	).is_equal_approx(Color.white)


func _scan_files(path: String, extension: String, recursive: bool) -> Array:
	var files := []
	var dir := Directory.new()
	if dir.open(path) != OK:
		return files
	dir.list_dir_begin(true, true)
	while true:
		var name := dir.get_next()
		if name.empty():
			break
		var full_path := path + name
		if dir.current_is_dir():
			if recursive:
				files.append_array(_scan_files(full_path + "/", extension, true))
		elif name.get_extension() == extension:
			files.append(full_path)
	dir.list_dir_end()
	return files


func _get_project_dir() -> String:
	var project_dir := str(request.get("project_dir", ""))
	if project_dir.empty():
		_add_error("Missing project_dir.")
		return ""
	if not project_dir.ends_with("/"):
		project_dir += "/"
	var dir := Directory.new()
	if not dir.dir_exists(project_dir):
		_add_error("Project directory does not exist: %s" % project_dir)
		return ""
	return project_dir


func _get_or_create_project_dir() -> String:
	var project_dir := str(request.get("project_dir", ""))
	if project_dir.empty():
		_add_error("Missing project_dir.")
		return ""
	if not project_dir.ends_with("/"):
		project_dir += "/"
	if not _ensure_dir(project_dir):
		return ""
	return project_dir


func _get_required_path(key: String) -> String:
	var path := str(request.get(key, ""))
	if path.empty():
		_add_error("Missing %s." % key)
		return ""
	var file := File.new()
	if not file.file_exists(path):
		_add_error("File does not exist: %s" % path)
		return ""
	return path


func _get_output_path(key: String) -> String:
	var path := str(request.get(key, ""))
	if path.empty():
		_add_error("Missing %s." % key)
	return path


func _vector_dict(value: Vector2) -> Dictionary:
	return {"x": value.x, "y": value.y}


func _request_vector(key: String, default_value: Vector2) -> Vector2:
	if not request.has(key) or typeof(request[key]) != TYPE_DICTIONARY:
		return default_value
	var value: Dictionary = request[key]
	return Vector2(float(value.get("x", default_value.x)), float(value.get("y", default_value.y)))


func _ensure_dir(path: String) -> bool:
	var dir := Directory.new()
	if dir.dir_exists(path):
		return true
	if dir.make_dir_recursive(path) != OK:
		_add_error("Could not create directory: %s" % path)
		return false
	return true


func _safe_file_name(value: String) -> String:
	return value.get_file().replace(" ", "_")


func _copy_png(source_path: String, target_path: String) -> bool:
	var image := Image.new()
	if image.load(source_path) != OK:
		_add_error("Could not load PNG: %s" % source_path)
		return false
	if image.save_png(target_path) != OK:
		_add_error("Could not write PNG: %s" % target_path)
		return false
	return true


func _copy_text_file(source_path: String, target_path: String) -> bool:
	var input := File.new()
	if input.open(source_path, File.READ) != OK:
		_add_error("Could not open source file: %s" % source_path)
		return false
	var text := input.get_as_text()
	input.close()

	var output := File.new()
	if output.open(target_path, File.WRITE) != OK:
		_add_error("Could not write target file: %s" % target_path)
		return false
	output.store_string(text)
	output.close()
	return true


func _add_error(message: String):
	response["errors"].append(message)


func _write_response():
	if response_path.empty():
		return
	var file := File.new()
	if file.open(response_path, File.WRITE) == OK:
		file.store_string(JSON.print(response, "\t"))
		file.close()


func _finish(exit_code: int):
	_write_response()
	quit(exit_code)
