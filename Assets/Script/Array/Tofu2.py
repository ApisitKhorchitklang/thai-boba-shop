P1 = 1
P2 = 2
GOAL = 19


initial_state = (0, 0, P1)

def successor(state):
    successors = []

    p1_pos, p2_pos, turn = state

    if turn == P1:
        current_pos = p1_pos
        other_pos = p2_pos
        next_turn = P2
    else:
        current_pos = p2_pos
        other_pos = p1_pos
        next_turn = P1

    dice_results = [1,2,3,4,2,3,4,5,6,7,8]
    directions = [1, -1] 

    for d in dice_results:
        for direction in directions:
            new_pos = current_pos + direction * d

            if new_pos < 0 or new_pos > GOAL:
                continue

            if turn == P1:
                new_state = (new_pos, other_pos, next_turn)
            else:
                new_state = (other_pos, new_pos, next_turn)

            successors.append(new_state)

    return successors

def is_terminal(state):
    p1_pos, p2_pos, _ = state
    return p1_pos == GOAL or p2_pos == GOAL

s = initial_state
next_states = successor(s)

print("Initial state:", s)
print("Successor states:")
for ns in next_states:
    print(ns)
print("Is terminal:", is_terminal(s))
