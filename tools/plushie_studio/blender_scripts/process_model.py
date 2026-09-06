import bpy
import mathutils
import sys
import os
import argparse

def parse_args():
    # Os argumentos após '--' são os nossos
    argv = sys.argv
    if "--" not in argv:
        args_to_parse = []
    else:
        args_to_parse = argv[argv.index("--") + 1:]

    parser = argparse.ArgumentParser(description="Processador Automático de Modelos 3D para GarraMania")
    parser.add_argument("--input", required=True, help="Caminho do arquivo de entrada (.glb, .gltf, .obj)")
    parser.add_argument("--output_fbx", required=True, help="Caminho de saída para .fbx")
    parser.add_argument("--output_blend", required=False, default="", help="Caminho de saída para .blend")
    parser.add_argument("--output_preview", required=False, default="", help="Caminho para thumbnail .png")
    parser.add_argument("--output_tex", required=False, default="", help="Caminho para textura extraída .png")
    parser.add_argument("--target_height", type=float, default=0.55, help="Altura alvo em metros (padrão: 0.55m)")
    parser.add_argument("--max_poly", type=int, default=25000, help="Limite de polígonos para otimização")
    parser.add_argument("--model_name", type=str, default="Prize_Plushie", help="Nome do objeto")

    return parser.parse_args(args_to_parse)

def main():
    args = parse_args()
    print(f"--- [GarraMania Blender Processor] Processando: {args.input} ---")

    # Limpa a cena do Blender
    bpy.ops.wm.read_factory_settings(use_empty=True)

    # Importa conforme extensão
    ext = os.path.splitext(args.input)[1].lower()
    if ext in [".glb", ".gltf"]:
        bpy.ops.import_scene.gltf(filepath=args.input)
    elif ext == ".obj":
        bpy.ops.wm.obj_import(filepath=args.input)
    elif ext == ".fbx":
        bpy.ops.import_scene.fbx(filepath=args.input)
    else:
        print(f"ERRO: Formato não suportado: {ext}")
        sys.exit(1)

    mesh_objs = [obj for obj in bpy.context.scene.objects if obj.type == 'MESH']
    if not mesh_objs:
        print("ERRO: Nenhuma malha (mesh) encontrada no arquivo importado!")
        sys.exit(1)

    # Seleciona todas as malhas
    bpy.ops.object.select_all(action='DESELECT')
    for obj in mesh_objs:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = mesh_objs[0]

    # Junta em um único mesh se houver múltiplos fragmentos
    if len(mesh_objs) > 1:
        bpy.ops.object.join()
        main_obj = bpy.context.view_layer.objects.active
    else:
        main_obj = mesh_objs[0]

    main_obj.name = args.model_name

    # Aplica rotação e escala originais
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)

    # Calcula bounding box atual no espaço mundial
    bbox_corners = [main_obj.matrix_world @ mathutils.Vector(corner) for corner in main_obj.bound_box]
    min_x = min(v.x for v in bbox_corners)
    max_x = max(v.x for v in bbox_corners)
    min_y = min(v.y for v in bbox_corners)
    max_y = max(v.y for v in bbox_corners)
    min_z = min(v.z for v in bbox_corners)
    max_z = max(v.z for v in bbox_corners)

    current_height = max_z - min_z
    print(f"Dimensões originais: X={max_x-min_x:.3f}m, Y={max_y-min_y:.3f}m, Altura Z={current_height:.3f}m")

    # 1. Normaliza Escala para altura de pelúcia (target_height)
    if current_height > 0.001:
        scale_factor = args.target_height / current_height
        main_obj.scale = (scale_factor, scale_factor, scale_factor)
        bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)

    # 2. Centraliza Pivô na base dos pés (Z = 0, X = 0, Y = 0)
    bbox_corners = [main_obj.matrix_world @ mathutils.Vector(corner) for corner in main_obj.bound_box]
    center_x = (min(v.x for v in bbox_corners) + max(v.x for v in bbox_corners)) / 2.0
    center_y = (min(v.y for v in bbox_corners) + max(v.y for v in bbox_corners)) / 2.0
    bottom_z = min(v.z for v in bbox_corners)

    main_obj.location.x -= center_x
    main_obj.location.y -= center_y
    main_obj.location.z -= bottom_z
    bpy.ops.object.transform_apply(location=True, rotation=False, scale=False)
    print("Pivô calibrado para a base dos pés (Z=0, X=0, Y=0).")

    # 3. Otimização de Polígonos (Decimate)
    total_poly = len(main_obj.data.polygons)
    print(f"Polígonos iniciais: {total_poly}")
    if total_poly > args.max_poly:
        ratio = max(0.2, float(args.max_poly) / float(total_poly))
        print(f"Aplicando Decimate com ratio={ratio:.2f}...")
        mod = main_obj.modifiers.new(name="Decimate_Auto", type='DECIMATE')
        mod.ratio = ratio
        bpy.ops.object.modifier_apply(modifier="Decimate_Auto")
        print(f"Polígonos pós-decimate: {len(main_obj.data.polygons)}")

    # 4. Processamento de Texturas e Cores (Bake automático de Vertex Color para PNG / UVs)
    has_image = False
    found_image = None
    for mat_slot in main_obj.material_slots:
        if mat_slot.material and mat_slot.material.use_nodes:
            for node in mat_slot.material.node_tree.nodes:
                if node.type == 'TEX_IMAGE' and node.image:
                    found_image = node.image
                    has_image = True
                    break
        if has_image:
            break

    has_vertex_colors = len(main_obj.data.color_attributes) > 0
    print(f"Diagnóstico de Materiais: Tem Imagem={has_image}, Tem Cores por Vértice={has_vertex_colors}")

    if has_vertex_colors and not has_image:
        print("Modelagem com Cores por Vértice detectada (TripoSR). Gerando UVs e assando textura PNG (Cycles Bake)...")
        # 1. Garante seleção isolada do mesh
        bpy.ops.object.select_all(action='DESELECT')
        main_obj.select_set(True)
        bpy.context.view_layer.objects.active = main_obj

        # 2. Desembrulha mapa UV se necessário
        if not main_obj.data.uv_layers:
            bpy.ops.object.mode_set(mode='EDIT')
            bpy.ops.mesh.select_all(action='SELECT')
            bpy.ops.uv.smart_project(angle_limit=66.0, margin_method='SCALED', island_margin=0.01)
            bpy.ops.object.mode_set(mode='OBJECT')
            print("Mapa UV gerado com sucesso!")

        # 3. Cria imagem de textura para o bake
        tex_image = bpy.data.images.new(name=f"{args.model_name}_Texture", width=1024, height=1024, alpha=False)

        # 4. Configura nós de emissão para o bake
        if not main_obj.material_slots or not main_obj.material_slots[0].material:
            mat = bpy.data.materials.new(name=f"Mat_{args.model_name}")
            main_obj.data.materials.append(mat)
        else:
            mat = main_obj.material_slots[0].material

        mat.use_nodes = True
        nodes = mat.node_tree.nodes
        nodes.clear()

        color_attr_name = main_obj.data.color_attributes[0].name
        vcol_node = nodes.new('ShaderNodeVertexColor')
        vcol_node.layer_name = color_attr_name

        emit_node = nodes.new('ShaderNodeEmission')
        out_node = nodes.new('ShaderNodeOutputMaterial')
        tex_node = nodes.new('ShaderNodeTexImage')
        tex_node.image = tex_image
        nodes.active = tex_node

        mat.node_tree.links.new(vcol_node.outputs['Color'], emit_node.inputs['Color'])
        mat.node_tree.links.new(emit_node.outputs['Emission'], out_node.inputs['Surface'])

        # 5. Executa bake de emissão no Cycles
        bpy.context.scene.render.engine = 'CYCLES'
        bpy.context.scene.cycles.device = 'CPU'
        bpy.context.scene.cycles.samples = 1
        bpy.context.scene.cycles.bake_type = 'EMIT'
        try:
            bpy.ops.object.bake(type='EMIT')
            print("Bake de textura concluído com sucesso!")
        except Exception as e_bake:
            print(f"Aviso no bake: {e_bake}")

        # 6. Salva textura PNG
        if args.output_tex:
            os.makedirs(os.path.dirname(os.path.abspath(args.output_tex)), exist_ok=True)
            tex_image.filepath_raw = args.output_tex
            tex_image.file_format = 'PNG'
            tex_image.save()
            print(f"Textura assada e salva em: {args.output_tex}")

        # 7. Reconecta material final com Principled BSDF e textura de imagem
        nodes.clear()
        principled = nodes.new('ShaderNodeBsdfPrincipled')
        principled.inputs['Roughness'].default_value = 0.75
        principled.inputs['Specular IOR Level'].default_value = 0.1
        tex_final = nodes.new('ShaderNodeTexImage')
        tex_final.image = tex_image
        out_final = nodes.new('ShaderNodeOutputMaterial')
        mat.node_tree.links.new(tex_final.outputs['Color'], principled.inputs['Base Color'])
        mat.node_tree.links.new(principled.outputs['BSDF'], out_final.inputs['Surface'])

    elif found_image and args.output_tex:
        os.makedirs(os.path.dirname(os.path.abspath(args.output_tex)), exist_ok=True)
        found_image.filepath_raw = args.output_tex
        found_image.file_format = 'PNG'
        found_image.save()
        print(f"Textura existente salva em: {args.output_tex}")

    # 5. Salva arquivo .blend se especificado
    if args.output_blend:
        os.makedirs(os.path.dirname(os.path.abspath(args.output_blend)), exist_ok=True)
        bpy.ops.wm.save_as_mainfile(filepath=args.output_blend)
        print(f"Arquivo .blend salvo em: {args.output_blend}")

    # 6. Exporta FBX otimizado para Unity
    os.makedirs(os.path.dirname(os.path.abspath(args.output_fbx)), exist_ok=True)
    bpy.ops.export_scene.fbx(
        filepath=args.output_fbx,
        check_existing=False,
        use_selection=True,
        global_scale=1.0,
        apply_unit_scale=True,
        apply_scale_options='FBX_SCALE_ALL',
        bake_space_transform=True,
        axis_forward='-Z',
        axis_up='Y',
        path_mode='COPY',
        embed_textures=True
    )
    print(f"Arquivo FBX exportado com sucesso em: {args.output_fbx}")

    # 7. Renderização do Thumbnail de Preview 360
    if args.output_preview:
        os.makedirs(os.path.dirname(os.path.abspath(args.output_preview)), exist_ok=True)
        bpy.ops.object.camera_add(location=(0.9, -1.2, 0.75), rotation=(1.1, 0, 0.65))
        cam = bpy.context.active_object
        bpy.context.scene.camera = cam

        bpy.ops.object.light_add(type='SUN', location=(2, -2, 4))
        sun = bpy.context.active_object
        sun.data.energy = 4.0
        sun.rotation_euler = (0.7, 0.2, 0.8)

        bpy.ops.object.light_add(type='POINT', location=(-1, -1, 1.5))
        fill = bpy.context.active_object
        fill.data.energy = 160.0

        bpy.context.scene.render.resolution_x = 512
        bpy.context.scene.render.resolution_y = 512
        bpy.context.scene.render.filepath = args.output_preview
        bpy.ops.render.render(write_still=True)
        print(f"Preview renderizado em: {args.output_preview}")

    print("--- [GarraMania Blender Processor] Sucesso Total! ---")

if __name__ == "__main__":
    main()
