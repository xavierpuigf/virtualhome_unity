import argparse
import os
import re

SCENEINFO = 'sceneinfo.txt'

def updateDictionary(ptrn, dict, line):
  result = ptrn.match(line)
  if result:
    key = result.group(1)
    if key in dict:
      dict[key] += 1
    else:
      dict[key] = 1

def printProportion(dict):
  s = 0
  for k, v in dict.items():
    s += v

  if s != 0:
    for k, v in dict.items():
      print( '{}: {} ({:.2f}%)'.format(k, v, v / s * 100) )

  print('Total: {}\n'.format(s))

parser = argparse.ArgumentParser()

parser.add_argument('directory', help='Absolute path to the directory of interest')
args = parser.parse_args()

dic_char = {}
dic_scene = {}

ptrn_char = re.compile(r'\s+"characterName": "(.*)",?')
ptrn_scene = re.compile(r'\s+"sceneName": "(.*)",?')

for folder in os.listdir(args.directory):
  if folder[0] != '.' and folder != 'scripts':
    f = open(args.directory + folder + '/' + SCENEINFO, 'r')

    for line in f:
      if line[0] != '{' and line[0] != '}':
        updateDictionary(ptrn_char, dic_char, line)
        updateDictionary(ptrn_scene, dic_scene, line)

printProportion(dic_char)
printProportion(dic_scene)
