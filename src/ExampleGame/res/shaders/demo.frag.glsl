#version 330 core

out vec4 FragColor;
uniform float _MY_SHADER_PROPERTY;

void main() {
    FragColor = vec4(_MY_SHADER_PROPERTY, 0.2, 0.8, 1.0);
}
