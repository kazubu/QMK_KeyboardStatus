# QMK Keyboard Status

A Windows taskbar indicator that shows the currently active layer on a QMK-based keyboard.
In addition, this program changes the color of mouse pointer during layer is changed. It's useful with Auto Mouse Layer feature that is used in pointing device integrated keyboards.

This program changes the mouse pointer color, icon color and tooltip in the Windows taskbar based on layer change notifications received via the HID console. Note that a modification to the keyboard firmware is required to send layer change events through the HID console ([example](https://github.com/kazubu/qmk-userspace-vial/blob/dfbad24f6e3ccde04e80361c52b4dc3aadc1f5f9/keyboards/mck/trackballseries/47/keymaps/vial/keymap.c#L198)).

![image](https://raw.githubusercontent.com/kazubu/QMK_KeyboardStatus/images/image1.gif)
