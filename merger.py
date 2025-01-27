import re
import os
import sys

namespace_ignore = ""
namespace_must_have = ""


def clean_bracket_str(dirty_str: str) -> str:
    clean_str = ""
    bracket_counter = 0
    for c in dirty_str:
        clean_str += c
        if c == '{':
            bracket_counter += 1
        elif c == '}':
            bracket_counter -= 1
            if bracket_counter == 0:
                return clean_str
    return clean_str


def merge_cs_files(directory_path: str) -> str:
    # Regular expressions to match namespaces and classes
    namespace_pattern = re.compile(r'namespace (.+?)[\r\n]')
    class_pattern = re.compile(
        r'(\/\/\/ <summary>[\S\s]+?<\/summary>\s+)*(\[.+\]\s+)*(public|internal) class (.+?)[\r\n][\S\s]*\}')

    # Dictionary to store classes based on their namespaces
    classes_by_namespace = {}
    output = ""

    # Iterate through each C# file in the specified directory
    for filename in os.listdir(directory_path):
        file_path = os.path.join(directory_path, filename)
        if filename.endswith(".cs"):
            print(f"Reading file {filename}")

            with open(file_path, 'r') as file:
                content = file.read()
                namespace_match = namespace_pattern.search(content)
                class_matches = class_pattern.finditer(content)
                namespace = ''

                if namespace_match:
                    namespace = namespace_match.group(1)
                    must_ignore = (len(namespace_ignore) > 0
                                   and namespace.find(namespace_ignore) >= 0) or (len(namespace_must_have) and namespace.find(namespace_must_have) < 0)
                    if must_ignore:
                        print(
                            f"     Found a namespace match: {namespace} -> Ignored")
                        continue

                    print(f"     Found a namespace match: {namespace}")
                    if namespace not in classes_by_namespace:
                        classes_by_namespace[namespace] = []

                for match in class_matches:
                    class_name = match.group(4)
                    whole_class_str = clean_bracket_str(
                        content[match.start():match.end()])
                    if namespace in classes_by_namespace:
                        classes_by_namespace[namespace].append(whole_class_str)
                        print(f"          Found a CLASS match: {class_name}")
        elif os.path.isdir(file_path):
            print(f"Found subdirectory: {filename}/")
            output += merge_cs_files(file_path)

    for namespace, classes in classes_by_namespace.items():
        output += f'namespace {namespace}\n{{\n'
        for class_content in classes:
            output += f'    {class_content}\n'
        output += '}\n\n'
    return output


args = sys.argv

if len(args) <= 1:
    print("Need to enter a folder with the code.")
    exit()

if args[1] == "-help" or args[1] == "-h":
    print("Merger arguments use:\n<source_folder> [<destination_file_path>] [-x <namespace_to_be_ignored>] [-m <must_have_this_namespace>]")
    exit()

path = args[1]
result = os.path.join(path, "merged_output.cs")
if len(args) > 2:
    if os.path.isdir(args[2]):
        result = os.path.join(args[2], "merged_output.cs")
    else:
        result = args[2]

if len(args) > 3:
    for index in range(3, len(args)):
        match args[index]:
            case "-x":
                if index + 1 >= len(args):
                    print("Error: -x expects the name of a namespace to be ignored.")
                    continue
                namespace_ignore = args[index + 1]
                index += 1
            case "-m":
                if index + 1 >= len(args):
                    print(
                        "Error: -m expects a string that must be in the name of the namespace.")
                    continue
                namespace_must_have = args[index + 1]
                index += 1


print(f"Executing merge with: {path} into {result}")

# Write the merged content to the output file
with open(result, 'w') as output:
    output.write(merge_cs_files(path))
