import sys
import numpy as np
from celluloid import Camera
import pdb
import scipy
from ast import literal_eval as make_tuple
import math
import json
import ipdb
from tqdm import tqdm
import matplotlib.pyplot as plt
from matplotlib.collections import PatchCollection
from scipy.spatial.transform import Rotation as R
from matplotlib.patches import Rectangle
import cv2 

def get_angle2(pos1, pos2):
    dx = pos1[0] - pos2[0]
    dy = pos1[2] - pos2[2]
    return 180./math.pi * np.arctan2(dy, dx)

def get_angle(rot):
    rot = R.from_quat(rot)
    euler = rot.as_euler('xzy')
    # dchange = np.sin(euler[1])*np.cos(euler[0]), np.cos(euler[1])*np.sin(euler[0])
    # dchange = np.sin(euler[1]+euler[0]), np.cos(euler[1]+euler[0])
    x = np.cos(euler[2])*np.cos(euler[1])
    y = np.sin(euler[2])*np.cos(euler[1])
    z = np.sin(euler[1])
    dchange = y, x
    return np.arctan2(x,y)*180 / math.pi
    
def plot_graph_2d(graph, ax, goal_ids, belief_ids=[], c_obs_ids=None):


    #nodes_interest = [node for node in graph['nodes'] if 'GRABBABLE' in node['properties']]
    goals = [node for node in graph['nodes'] if node['class_name'] in goal_ids]
    
    belief_obj = [node for node in graph['nodes'] if node['id'] in belief_ids]

    # container_surf = dict_info['objects_inside'] + dict_info['objects_surface']
#     pdb.set_trace()
    container_surf = ['kitchentable', 'cabinet', 'kitchencabinet', 'kitchencabinets', 'fridge', 'bathroomcabinet', 'stove', 'coffeetable', 'dishwasher']
    container_and_surface = [node for node in graph['nodes'] if node['class_name'] in container_surf]
    container_open = [node for node in graph['nodes'] if node['class_name'] in container_surf and 'OPEN' in node['states']]
    container_open_id = [n['id'] for n in container_open]
    c_obs_ids= []
    obs_objects = []
    if len(container_open_id) > 0:
      c_obs_ids = [c for c in c_obs_ids if c not in container_open_id]
      obs_objects = [node for node in graph['nodes'] if node['id'] in c_obs_ids and node['category'] not in ['Rooms', 'Doors'] and node['class_name'] != 'character']
    
    node_char = [node for node in graph['nodes'] if node['id'] == 1][0]
#     for ob in obs_objects:
#         pos_char = node_char['obj_transform']['position']
#         angle_char = get_angle(node_char['obj_transform']['rotation'])
#         print(ob['class_name'], get_angle2(ob['obj_transform']['position'], pos_char), angle_char)
    
    #grabbed_obj = [node for node in graph['nodes'] if node['class_name'] in dict_info['objects_grab']]
    rooms = [node for node in graph['nodes'] if 'Rooms' == node['category']]


    # containers and surfaces
    # visible_nodes = [node for node in graph['nodes'] if node['id'] in visible_ids and node['category'] != 'Rooms']
    # action_nodes = [node for node in graph['nodes'] if node['id'] in action_ids and node['category'] != 'Rooms']

    # goal_nodes = [node for node in graph['nodes'] if node['class_name'] == 'cupcake']

    # Character
    # char_node = [node for node in graph['nodes'] if node['id'] == char_id][0]

    
    add_boxes(rooms, ax, points=None, rect={'alpha': 0.1})
    
        
    if len(container_and_surface) > 0:
        add_boxes(container_and_surface, ax, points=None, rect={'fill': False, 'edgecolor': 'blue', 'alpha': 0.3})
        
    if len(container_open) > 0:
#         print("HERE")
        add_boxes(container_open, ax, points=None, rect={'fill': False, 'edgecolor': 'orange', 'alpha': 1.0})
        
    #add_boxes([char_node], ax, points=None, rect={'facecolor': 'yellow', 'edgecolor': 'yellow', 'alpha': 0.7})
    #add_boxes(visible_nodes, ax, points={'s': 2.0, 'alpha': 1.0}, rect={'fill': False,
    #                     
    
    #add_boxes(action_nodes, ax, points={'s': 3.0, 'alpha': 1.0, 'c': 'red'})


    #bad_classes = ['character']
    if len(obs_objects):
        add_boxes(obs_objects, ax, points=None, rect={'fill': False, 'edgecolor': 'green', 'alpha': 0.5})
    
    if len(goals) > 0:
        add_boxes(goals, ax, points={'s':  40.0, 'alpha': 1.0, 'edgecolors': 'magenta', 'facecolors': 'none', 'linewidth': 1.0})
    if len(belief_obj) > 0:
        add_boxes(belief_obj, ax, points={'s':  30.0, 'alpha': 1.0, 'edgecolors': 'blue', 'facecolors': 'none', 'linewidth': 1.0})
    
    ax.set_aspect('equal')
    bx, by = get_bounds([room['bounding_box'] for room in rooms])

    maxsize = max(bx[1] - bx[0], by[1] - by[0])
    gapx = (maxsize - (bx[1] - bx[0])) / 4.
    gapy = (maxsize - (by[1] - by[0])) / 4.

    ax.set_xlim(bx[0]-gapx, bx[1]+gapx)
    ax.set_ylim(by[0]-gapy, by[1]+gapy)
    ax.apply_aspect()
    
def add_box(nodes, args_rect):
    rectangles = []
    centers = [[], []]
    for node in nodes:
        cx, cy = node['bounding_box']['center'][0], node['bounding_box']['center'][2]
        w, h = node['bounding_box']['size'][0], node['bounding_box']['size'][2]
        minx, miny = cx - w / 2., cy - h / 2.
        centers[0].append(cx)
        centers[1].append(cy)
        if args_rect is not None:
            rectangles.append(
                Rectangle((minx, miny), w, h, **args_rect)
            )
    return rectangles, centers


def add_boxes(nodes, ax, points=None, rect=None):
    rectangles = []
    rectangles_class, center = add_box(nodes, rect)
    rectangles += rectangles_class
    if points is not None:
        ax.scatter(center[0], center[1], **points)
    if rect is not None:
        ax.add_patch(rectangles[0])
        collection = PatchCollection(rectangles, match_original=True)
        ax.add_collection(collection)
        
def get_bounds(bounds):
    minx, maxx = None, None
    miny, maxy = None, None
    for bound in bounds:
        bgx, sx = bound['center'][0] + bound['size'][0] / 2., bound['center'][0] - bound['size'][0] / 2.
        bgy, sy = bound['center'][2] + bound['size'][2] / 2., bound['center'][2] - bound['size'][2] / 2.
        minx = sx if minx is None else min(minx, sx)
        miny = sy if miny is None else min(miny, sy)
        maxx = bgx if maxx is None else max(maxx, bgx)
        maxy = bgy if maxy is None else max(maxy, bgy)
    return (minx, maxx), (miny, maxy)

def visualize_trajectory(file_path, gen_vid=False, plot_belief=False, belief_id=None, 
                         full_obs=False, plot_img=False, frame_end=-1, plot_special_actions=False):
    char_id = 1
    with open(file_path, "r") as f:
        lines = f.readlines()
        graph = None
        obs = []
        graph_store = False
        pos_done = False
        graphs = {}
        for l in range(len(lines)):
            line = lines[l][:-1]
            if graph_store:

                graphInfo = json.loads(lines[l])
                graphs[int(graphInfo[-1])+1] = json.loads(graphInfo[0])

            # print(line)
            if line == "Script Action Data:":
                pos_done = True
            if line != "Position and Orientation Data:" and not pos_done:
                (chid, pos, orientation, time) = make_tuple(line)
                # print(pos)
                line = line[1:-1]
                obs.append((chid, pos, orientation, time))
                # pos, orientation_time = line.split(', (')
                # orientation, time = orientation_time.split('), ')
                # obs.append((tuple(map(float, pos[1:-1].split(', '))), tuple(map(float, orientation.split(', '))), float(time)))
            if line == "Graph Data:":
                graph_store = True
                # break
    if belief_id is None:
        plot_belief = False
    
    # Get xy coordinates of the agent
    print(graphs.keys())
    if 'obj_transform' in graphs[0]['nodes'][0]:
        #rots = [get_angle(ob[1]) for ob in obs]
        rots = None
        xy = np.array([[ob[0], ob[1][0],ob[1][2]] for ob in obs])
    else:
        coords = [[node['bounding_box'] for node in obs['nodes'] if node['id'] == char_id][0] for obs in observations]
        rots = None
        xy = np.array([[ob[0], ob[1][0],ob[1][2]] for ob in obs])
        
    # print(xy)
    n = 150
    colors = plt.cm.jet(np.linspace(0,1,n))
    if plot_img:
        fig = plt.figure(figsize=(12,6))
#         fig, axes = plt.subplots(2)
#         ax = axes[0]
#         ax_img = axes[1]
        grid = plt.GridSpec(2, 3, wspace=0.1, hspace=0.1)
        
        ax = fig.add_subplot(grid[:, :2])
        ax.axis('off')
        ax_img = fig.add_subplot(grid[1, 2])
        ax_img.axis('off')

    # Plot other info, only relevant for the planenr
    elif not plot_belief:
        fig = plt.figure(figsize=(12,9))
        ax = plt.axes()
        plt.axis('off')
    else:
        fig = plt.figure(figsize=(12,6))
        grid = plt.GridSpec(2, 3, wspace=0.1, hspace=0.1)
        id_object = belief_id
        if False:
            try:
                #pass
                id_object = int(content['subgoals'][0][-1][0].split('_')[1])
            except:
                #pass
                id_object = int(content['subgoals'][0][0][-1].split('_')[1])
        ax = fig.add_subplot(grid[:, :2])
        ax.axis('off')
        ax_belief = fig.add_subplot(grid[1, 2])
        id2name = {node['id']: node['class_name'] for node in content['init_unity_graph']['nodes']}
    
    if gen_vid:
        camera = Camera(fig)
    steps = len(xy)
    steps_total = list(range(len(xy)))
#     steps_total = steps_total[-10:]
    if not gen_vid:
        steps_total = [len(xy)-1]
        if frame_end != -1:
            steps_total = [frame_end]
        
    if plot_img:
        steps_total[-1] = steps_total[-1] - 1
    
    for steps_t in tqdm(steps_total):
            
        # Plot the scene
        plot_graph_2d(graphs[steps_t], ax, [])
        if plot_belief:
            belief_object = content['belief'][0][steps_t]
            do_plot_belief(belief_object, id2name, id_object, ax_belief)
        
        current_xy = xy[:steps_t, :]
        its = steps_t
        for agent_id in range(2):
            chxy = xy[:steps_t, :]
            chxy = chxy[chxy[:, 0] == agent_id][:, 1:]
            # print(chxy.shape)
            # ax.scatter(cxy[:,0], cxy[:, 1], color=colors[its], s=50, marker= (3, 0, 270+angle))
            chsteps = chxy.shape[0]
            if chsteps == 0:
                continue
            print(chxy.shape)
            for steps in range(chsteps):
                it = steps

                if it > 0:
                    cxy = chxy[it-1:it+1,:]
                    ax.plot(cxy[:,0], cxy[:, 1], '--', color=colors[min(it, len(colors)-1)], )
                    if not gen_vid:
                        ax.plot(cxy[-1:,0], cxy[-1:, 1], '.', color=colors[min(it, len(colors)-1)], )
                    

            if plot_special_actions:
                steps_grab = [ls for ls in range(steps_t) if 'grab' in content['action'][0][ls]]
                steps_open = [ls for ls in range(steps_t) if 'open' in content['action'][0][ls]]
                # Grab actions
                ax.plot(xy[steps_grab,0], xy[steps_grab, 1], '*', color='limegreen', )
                # Put actions
                ax.plot(xy[steps_open,0], xy[steps_open, 1], 'p', color='y', )
            
            
            cxy = chxy[chsteps-1:chsteps,:]
            if rots is not None:
                angle = rots[its]
                ax.scatter(cxy[:,0]+0.3*np.cos(angle*np.pi/180.), cxy[:, 1]+0.3*np.sin(angle*np.pi/180), color='red', s=15)

            else:
                angle = 0
            
            ax.scatter(cxy[:,0], cxy[:, 1], color='cyan', s=50)

        if gen_vid:
            print("Snap")
            camera.snap()
    if gen_vid:
        if '/' in file_path:
            dir_name, fname = file_path.split('/')[-2:]
        else:
            dir_name, fname = '', file_path
        fname = fname.replace('.txt', '')
        
        fn = '{}.mp4'.format(fname)
        print(fn)
        final = camera.animate()
        final.save(fn, fps=60)
        return final
    return None

def do_plot_belief(belief_object, id2name, id_object, currax):
#     currax.clear()
    belief_curr_object = belief_object[id_object]['INSIDE']
    names = belief_curr_object[0]
    probs = belief_curr_object[1]
    names = [id2name[name] if name is not None else 'None' for name in names]
    names = [name.replace('bathroom', 'b.').replace('kitchen', 'k.') for name in names]
    probs = scipy.special.softmax(probs)
    x = np.arange(len(names))
    currax.bar(x, probs, color='blue')
    currax.set_ylabel("Prob")
    currax.set_xticks(x)
    currax.grid(axis='y')
    currax.set_ylim([0,1])
    currax.set_xticklabels(names, rotation=40)

    
def get_location_objects(content, tstep=0):
    goal_obj = [x.split('_')[1] for x, y in content['goals'][0].items() if y > 0]
    graph = content['init_unity_graph']
    id2node = {node['id']: node for node in graph['nodes']}
    ids = [node['id'] for node in content['init_unity_graph']['nodes'] if node['class_name'] in goal_obj]
    locations = ['{} {} {}'.format(
        id2node[edge['from_id']]['class_name'],
        edge['relation_type'], 
        id2node[edge['to_id']]['class_name']) for edge in graph['edges'] if edge['from_id'] in ids]
    print("Locations")
    for loc in locations:
        print(loc)
    print("Beliefs")
    beliefs = content['belief'][0][tstep]
    print(ids)
    for idi in ids:
        
        beliefs_obj = beliefs[idi]['INSIDE']
        prob = scipy.special.softmax(beliefs_obj[1])
        names = ['None' if name is None else id2node[name]['class_name'] for name in beliefs_obj[0]]
        prob_loc = ['{}: {:.2f}'.format(names[it], prob[it]) for it in range(len(names))]
        prob_loc = ' '.join(prob_loc)
        print(id2node[idi]['class_name'], prob_loc)
        
     
filename = sys.argv[1]
visualize_trajectory(filename, gen_vid=True, plot_img=True, plot_belief=True, full_obs=True, frame_end=30)

