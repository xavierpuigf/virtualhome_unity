import json
import sys
import ipdb

graph_str = sys.argv[1]
with open(graph_str, 'r') as f:
	content = json.load(f)

message, graph = content['Item2'], content['Item1']
message = json.loads(message)



idi = int(graph_str.split('_')[-1].split('.')[0])
with open('Episodes/pilot_task_id_{}_bounds.json'.format(idi), 'r') as f:
	content_expand = json.load(f)

graph2 = content_expand['init_graph']
id2node_graph = {node['id']: node for node in graph['nodes']}
id2node_graph_2 = {node['id']: node for node in graph2['nodes']}


for unaligned_id in message['unaligned_ids']:
	if unaligned_id in id2node_graph:
		str_node = id2node_graph[unaligned_id]['class_name']
	else:
		str_node = ""
	print(unaligned_id, str_node, id2node_graph_2[unaligned_id]['prefab_name'])

ipdb.set_trace()


###### Delete mH objects #####
import glob
files = glob.glob('Episodes/*.json')
files = sorted(files)
for file in files:
	print(file)
	with open(file, 'r') as f:
		content = json.load(f)
		graph = content['init_graph']
		id2graph = {node['id']: node for node in graph['nodes']}
	ids_mh = [(node['id'], node['class_name']) for node in content['init_graph']['nodes'] if node['prefab_name'].startswith("mH_")]
	print(ids_mh)
	print('-------')



import glob
files = glob.glob('Episodes/*.json')
files = sorted(files)
for file in files:
	print(file)
	with open(file, 'r') as f:
		content = json.load(f)

	ids_mh = [(node['id'], node['class_name']) for node in content['init_graph']['nodes'] if node['prefab_name'].startswith("mH_")]
	print(ids_mh)
	print('-------')