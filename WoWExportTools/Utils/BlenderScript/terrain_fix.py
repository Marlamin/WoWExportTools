for obj in bpy.context.selected_objects:
    for mat_slot in obj.material_slots:
        node_tree = mat_slot.material.node_tree
        nodes = node_tree.nodes
        for node in nodes:
            if node.name != 'Image Texture' and node.name != 'Material Output' and node.name != 'Diffuse BSDF':
                node_tree.nodes.remove(node)
        node_tree.links.new(nodes['Image Texture'].outputs[0], nodes['Diffuse BSDF'].inputs[0])
        node_tree.links.new(nodes['Diffuse BSDF'].outputs[0], nodes['Material Output'].inputs[0])
        imageNode = nodes['Image Texture']
        imageNode.extension = 'EXTEND'
        imageNode.image.use_alpha = False