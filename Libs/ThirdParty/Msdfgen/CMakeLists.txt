cmake_minimum_required(VERSION 3.15)
project(DragonLibMsdfgen LANGUAGES CXX)
file(GLOB MSDFGEN_CORE CONFIGURE_DEPENDS core/*.cpp)
add_library(DragonLib.Msdfgen SHARED bridge.cpp ${MSDFGEN_CORE})
target_compile_features(DragonLib.Msdfgen PRIVATE cxx_std_11)
target_compile_definitions(DragonLib.Msdfgen PRIVATE MSDFGEN_PUBLIC= MSDFGEN_USE_CPP11)
set_target_properties(DragonLib.Msdfgen PROPERTIES
    MSVC_RUNTIME_LIBRARY "MultiThreaded$<$<CONFIG:Debug>:Debug>"
    CXX_VISIBILITY_PRESET hidden VISIBILITY_INLINES_HIDDEN YES)
