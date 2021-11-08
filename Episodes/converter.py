import json

file = 

with open(file, 'r') as file:
	ccontent = json.load(f)

with open(file_2, 'w+') as f:
	f.write(json.dumps(content, indent=4))