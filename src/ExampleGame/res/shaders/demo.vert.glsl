#version 330 core

layout(location = 0) in vec3 aPosition;
uniform mat4 _MVP;

void main() {
    gl_Position = _MVP * vec4(aPosition, 1.0);
}
