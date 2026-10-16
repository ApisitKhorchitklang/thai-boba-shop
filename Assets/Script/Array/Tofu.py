from typing import List, Tuple

State = Tuple[int, int, int] 

ARCS = {
    1: 4,
    7: 2,
    11: 15,
    16: 8
}

def successors(state: State) -> List[State]:
    p1, p2, player = state
    result = []

    MIN_POS = 0
    MAX_POS = 19

    current_pos = p1 if player == 1 else p2

    def apply_move(new_pos: int):
        
        if new_pos < MIN_POS or new_pos > MAX_POS:
            return

        if new_pos in ARCS:
            new_pos = ARCS[new_pos]

        if player == 1:
            result.append((new_pos, p2, 2))
        else:
            result.append((p1, new_pos, 1))

    for d in [1, 2, 3, 4]:
        for direction in (+1, -1):
            apply_move(current_pos + direction * d)

    for d1 in [1, 2, 3, 4]:
        for d2 in [1, 2, 3, 4]:
            d = d1 + d2
            for direction in (+1, -1):
                apply_move(current_pos + direction * d)

    return result

state = (1, 0, 1)  
next_states = successors(state)

for s in next_states:
    print(s)
print(state)