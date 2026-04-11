#version 330 core

layout(location = 0) in vec3 aPosition;
layout(location = 1) in vec3 aColor;

uniform mat4 _model_view_projection;

out vec3 vColor;

void main() {
    gl_Position = _model_view_projection * vec4(aPosition, 1.0);
    vColor = aColor;
}
